using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Domain;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.ViewModels;

namespace SkillBridge.Services;

/// <summary>
/// Assignment lifecycle: accept/decline, challenge registration, work submission,
/// employer review (approve / request revision / reject in challenge) and completion.
/// </summary>
public class AssignmentService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly FileStorageService _files;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;
    private readonly PaymentService _payments;

    public AssignmentService(ApplicationDbContext db, TimeProvider clock, FileStorageService files,
        NotificationService notifications, AuditService audit, PaymentService payments)
    {
        _db = db;
        _clock = clock;
        _files = files;
        _notifications = notifications;
        _audit = audit;
        _payments = payments;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private static readonly AssignmentStatus[] ActiveStatuses =
        { AssignmentStatus.PendingAcceptance, AssignmentStatus.InProgress, AssignmentStatus.Submitted, AssignmentStatus.RevisionRequested };

    // ---------------------------------------------------------------- queries

    public Task<TaskAssignment?> GetFullAsync(int id) =>
        _db.TaskAssignments
            .Include(a => a.Job).ThenInclude(j => j.Employer)
            .Include(a => a.Job).ThenInclude(j => j.Category)
            .Include(a => a.Hunter)
            .Include(a => a.Application)
            .Include(a => a.Submissions.OrderBy(s => s.Version))
            .Include(a => a.Payment)
            .Include(a => a.Ratings)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id);

    public static bool CanView(TaskAssignment a, string userId, bool isAdmin) =>
        isAdmin || a.HunterId == userId || a.Job.EmployerId == userId;

    public Task<List<TaskAssignment>> ListForHunterAsync(string hunterId) =>
        _db.TaskAssignments.AsNoTracking()
            .Include(a => a.Job).Include(a => a.Payment)
            .Where(a => a.HunterId == hunterId)
            .OrderByDescending(a => a.AssignedAt).ToListAsync();

    public Task<List<TaskAssignment>> ListForEmployerAsync(string employerId) =>
        _db.TaskAssignments.AsNoTracking()
            .Include(a => a.Job).Include(a => a.Hunter).Include(a => a.Payment)
            .Where(a => a.Job.EmployerId == employerId)
            .OrderByDescending(a => a.AssignedAt).ToListAsync();

    public async Task<List<WorkSubmission>> ChallengeSubmissionsAsync(int jobId) =>
        await _db.WorkSubmissions
            .Include(s => s.TaskAssignment).ThenInclude(a => a.Hunter)
            .Where(s => s.TaskAssignment.JobId == jobId)
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .ToListAsync();

    public async Task<ServiceResult<ChallengeReviewViewModel>> ChallengeReviewAsync(string employerId, bool isAdmin, int jobId)
    {
        var job = await _db.Jobs.AsNoTracking().Include(j => j.Assignments).ThenInclude(a => a.Hunter).FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null || job.Mode != JobMode.Challenge) return ServiceResult<ChallengeReviewViewModel>.Fail("Challenge not found.");
        if (!isAdmin && job.EmployerId != employerId) return ServiceResult<ChallengeReviewViewModel>.Fail("Not authorized.");
        var subs = await ChallengeSubmissionsAsync(jobId);
        return ServiceResult<ChallengeReviewViewModel>.Ok(new ChallengeReviewViewModel
        {
            Job = job,
            Participants = job.Assignments.OrderBy(a => a.AssignedAt).ToList(),
            Submissions = subs,
            NextReviewableSubmissionId = job.WinnerAssignmentId == null ? ChallengeRules.NextReviewableSubmissionId(subs) : null
        });
    }

    // ---------------------------------------------------------------- standard: accept / decline

    public async Task<ServiceResult> AcceptAsync(string hunterId, int assignmentId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var a = await _db.TaskAssignments.Include(x => x.Job).ThenInclude(j => j.Applications).Include(x => x.Application)
            .FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (a is null || a.HunterId != hunterId) return ServiceResult.Fail("Assignment not found.");
        if (a.Status != AssignmentStatus.PendingAcceptance) return ServiceResult.Fail("This assignment is not waiting for your acceptance.");
        if (a.Job.Status != JobStatus.Assigned) return ServiceResult.Fail("This job is no longer available.");

        a.Status = AssignmentStatus.InProgress;
        a.AcceptedAt = Now;
        var job = a.Job;
        job.Status = JobStatus.InProgress;
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();

        // Other competing applications are now unsuccessful.
        foreach (var other in job.Applications.Where(o => o.Id != a.ApplicationId && ApplicationRules.IsActive(o.Status)))
        {
            other.Status = ApplicationStatus.Rejected;
            other.UpdatedAt = Now;
            _notifications.Add(other.HunterId, NotificationType.ApplicationRejected, "Position filled",
                $"Another Hunter was selected for \"{job.Title}\". Thank you for applying.", job.Id, null, "/Applications/Mine");
        }

        _payments.CreateFor(a, job, PaymentStatus.Pending, hunterId);
        _notifications.Add(job.EmployerId, NotificationType.AssignmentStarted, "Assignment accepted",
            $"The selected Hunter accepted \"{job.Title}\". Work is now in progress.", job.Id, a.Id, $"/Assignments/Details/{a.Id}");
        _audit.Add(hunterId, "Assignment.Accept", nameof(TaskAssignment), a.Id);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("The job changed while you were accepting. Please reload.");
        }
        await tx.CommitAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeclineAsync(string hunterId, int assignmentId, string? reason)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var a = await _db.TaskAssignments.Include(x => x.Job).Include(x => x.Application).FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (a is null || a.HunterId != hunterId) return ServiceResult.Fail("Assignment not found.");
        if (a.Status != AssignmentStatus.PendingAcceptance) return ServiceResult.Fail("Only assignments awaiting acceptance can be declined.");

        a.Status = AssignmentStatus.Declined;
        if (a.Application != null)
        {
            a.Application.Status = ApplicationStatus.Withdrawn;
            a.Application.UpdatedAt = Now;
        }
        var job = a.Job;
        job.Status = Now < job.ApplicationDeadline ? JobStatus.Open : JobStatus.Closed;
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();
        _notifications.Add(job.EmployerId, NotificationType.AssignmentDeclined, "Assignment declined",
            $"The selected Hunter declined \"{job.Title}\". You can select another applicant. {reason}", job.Id, a.Id, $"/Applications/Compare/{job.Id}");
        _audit.Add(hunterId, "Assignment.Decline", nameof(TaskAssignment), a.Id, reason);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult.Ok();
    }

    // ---------------------------------------------------------------- challenge registration

    public async Task<ServiceResult<int>> JoinChallengeAsync(string hunterId, int jobId, bool acceptedTerms)
    {
        if (!acceptedTerms) return ServiceResult<int>.Fail("You must confirm that you have read and accept the challenge rules.");
        await using var tx = await _db.Database.BeginTransactionAsync();
        var job = await _db.Jobs.Include(j => j.Assignments).FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return ServiceResult<int>.Fail("Challenge not found.");
        if (job.EmployerId == hunterId) return ServiceResult<int>.Fail("You cannot join your own challenge.");
        if (job.Assignments.Any(a => a.HunterId == hunterId)) return ServiceResult<int>.Fail("You are already registered for this challenge.");
        var active = job.Assignments.Count(a => a.Status != AssignmentStatus.Cancelled);
        if (!ChallengeRules.CanJoin(job, active, Now, out var reason)) return ServiceResult<int>.Fail(reason!);

        var a = new TaskAssignment
        {
            JobId = job.Id,
            HunterId = hunterId,
            IsChallengeParticipant = true,
            AgreedAmount = job.Budget,
            AssignedAt = Now,
            AcceptedAt = Now,
            DueDate = job.ProjectDeadline,
            Status = AssignmentStatus.InProgress
        };
        _db.TaskAssignments.Add(a);
        if (active + 1 >= job.MaxParticipants) job.Status = JobStatus.InProgress; // all places filled
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();   // two Hunters racing for the last place -> one fails
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<int>.Fail("Someone registered at the same moment. Please reload and try again.");
        }
        catch (DbUpdateException)
        {
            return ServiceResult<int>.Fail("You are already registered for this challenge.");
        }

        _notifications.Add(hunterId, NotificationType.ChallengeJoined, "Challenge joined",
            $"You joined \"{job.Title}\". Submit before {job.ProjectDeadline.Local()}. Only the first submission that passes review wins.",
            job.Id, a.Id, $"/Assignments/Details/{a.Id}");
        _notifications.Add(job.EmployerId, NotificationType.ChallengeJoined, "New challenge participant",
            $"A Hunter joined your challenge \"{job.Title}\" ({active + 1}/{job.MaxParticipants}).", job.Id, a.Id, $"/Assignments/Challenge/{job.Id}");
        _audit.Add(hunterId, "Challenge.Join", nameof(TaskAssignment), a.Id, $"Job {job.Id}");
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<int>.Ok(a.Id);
    }

    // ---------------------------------------------------------------- submission

    public static bool CanSubmit(TaskAssignment a, DateTime utcNow) =>
        a.Status is AssignmentStatus.InProgress or AssignmentStatus.RevisionRequested
        && a.Job.Status is not (JobStatus.Cancelled or JobStatus.Completed)
        && (!a.IsChallengeParticipant || (utcNow <= a.Job.ProjectDeadline && a.Job.WinnerAssignmentId == null));

    public async Task<ServiceResult<int>> SubmitAsync(string hunterId, SubmitWorkModel f)
    {
        var a = await _db.TaskAssignments.Include(x => x.Job).Include(x => x.Submissions).Include(x => x.Payment)
            .FirstOrDefaultAsync(x => x.Id == f.AssignmentId);
        if (a is null || a.HunterId != hunterId) return ServiceResult<int>.Fail("Assignment not found.");
        if (!CanSubmit(a, Now))
            return ServiceResult<int>.Fail(a.IsChallengeParticipant && Now > a.Job.ProjectDeadline
                ? "The challenge submission deadline has passed."
                : "You cannot submit work for this assignment right now.");
        if (string.IsNullOrWhiteSpace(f.ExternalLink) && f.File is not { Length: > 0 })
            return ServiceResult<int>.Fail("Attach a file or provide a link to the work.");

        var sub = new WorkSubmission
        {
            TaskAssignmentId = a.Id,
            Version = a.Submissions.Count == 0 ? 1 : a.Submissions.Max(s => s.Version) + 1,
            Description = f.Description.Trim(),
            ExternalLink = string.IsNullOrWhiteSpace(f.ExternalLink) ? null : f.ExternalLink.Trim(),
            SubmittedAt = Now,  // server-side timestamp
            Status = SubmissionStatus.PendingReview
        };
        if (f.File is { Length: > 0 })
        {
            var saved = await _files.SaveAsync(f.File, "submissions");
            if (!saved.Succeeded) return ServiceResult<int>.Fail(saved.Error!);
            sub.FilePath = saved.Value!.RelativePath;
            sub.FileOriginalName = saved.Value.OriginalName;
        }
        a.Submissions.Add(sub);
        a.Status = AssignmentStatus.Submitted;
        if (!a.IsChallengeParticipant)
        {
            a.Job.Status = JobStatus.UnderReview;
            a.Job.UpdatedAt = Now;
            if (a.Payment != null) _payments.Transition(a.Payment, PaymentStatus.AwaitingApproval, hunterId, $"Work v{sub.Version} submitted");
        }
        _notifications.Add(a.Job.EmployerId, NotificationType.WorkSubmitted, "Work submitted",
            $"Version {sub.Version} of the work for \"{a.Job.Title}\" was submitted for your review.",
            a.JobId, a.Id, a.IsChallengeParticipant ? $"/Assignments/Challenge/{a.JobId}" : $"/Assignments/Details/{a.Id}");
        _audit.Add(hunterId, "Work.Submit", nameof(TaskAssignment), a.Id, $"Submission v{sub.Version}");
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<int>.Fail("A submission was saved at the same moment. Please reload.");
        }
        return ServiceResult<int>.Ok(sub.Id);
    }

    // ---------------------------------------------------------------- review

    private async Task<WorkSubmission?> LoadSubmissionAsync(int submissionId) =>
        await _db.WorkSubmissions
            .Include(s => s.TaskAssignment).ThenInclude(a => a.Job).ThenInclude(j => j.Assignments).ThenInclude(x => x.Submissions)
            .Include(s => s.TaskAssignment).ThenInclude(a => a.Payment)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == submissionId);

    /// <summary>Employer approves work. In challenge mode this also determines the winner.</summary>
    public async Task<ServiceResult> ApproveAsync(string employerId, int submissionId, string? feedback)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var sub = await LoadSubmissionAsync(submissionId);
        if (sub is null) return ServiceResult.Fail("Submission not found.");
        var a = sub.TaskAssignment;
        var job = a.Job;
        if (job.EmployerId != employerId) return ServiceResult.Fail("You can only review work for your own jobs.");
        if (sub.Status != SubmissionStatus.PendingReview) return ServiceResult.Fail("This submission has already been reviewed.");
        if (a.Status != AssignmentStatus.Submitted) return ServiceResult.Fail("This assignment is not awaiting review.");
        if (a.IsChallengeParticipant)
        {
            if (job.WinnerAssignmentId != null) return ServiceResult.Fail("This challenge already has a winner.");
            var all = job.Assignments.SelectMany(x => x.Submissions).ToList();
            if (ChallengeRules.NextReviewableSubmissionId(all) != sub.Id)
                return ServiceResult.Fail("Submissions must be reviewed in the order they were received. Review the earliest pending submission first.");
        }

        sub.Status = SubmissionStatus.Approved;
        sub.EmployerFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        sub.ReviewedAt = Now;
        a.Status = AssignmentStatus.Completed;
        a.CompletedAt = Now;
        job.Status = JobStatus.Completed;
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();

        if (a.IsChallengeParticipant)
        {
            job.WinnerAssignmentId = a.Id;
            a.IsWinner = true;
            foreach (var other in job.Assignments.Where(x => x.Id != a.Id && ActiveStatuses.Contains(x.Status)))
            {
                other.Status = AssignmentStatus.ChallengeLost;
                foreach (var s in other.Submissions.Where(s => s.Status == SubmissionStatus.PendingReview))
                {
                    s.Status = SubmissionStatus.Closed;
                    s.ReviewedAt = Now;
                }
                _notifications.Add(other.HunterId, NotificationType.ChallengeClosed, "Challenge closed",
                    $"Another participant's earlier submission passed review for \"{job.Title}\". The challenge is now closed.",
                    job.Id, other.Id, $"/Assignments/Details/{other.Id}");
            }
            _payments.CreateFor(a, job, PaymentStatus.Approved, employerId);
            _notifications.Add(a.HunterId, NotificationType.ChallengeWon, "You won the challenge!",
                $"Your submission for \"{job.Title}\" was the first to pass review. The reward of {a.AgreedAmount:N2} {job.Currency} is now due (simulated ledger).",
                job.Id, a.Id, $"/Assignments/Details/{a.Id}");
            _audit.Add(employerId, "Challenge.Winner", nameof(TaskAssignment), a.Id, $"Job {job.Id}, submission {sub.Id} at {sub.SubmittedAt:O}");
        }
        else
        {
            if (a.Payment != null) _payments.Transition(a.Payment, PaymentStatus.Approved, employerId, "Work approved");
            _notifications.Add(a.HunterId, NotificationType.WorkApproved, "Work approved",
                $"Your work for \"{job.Title}\" was approved. The job is complete and payment is now due.", job.Id, a.Id, $"/Assignments/Details/{a.Id}");
            _audit.Add(employerId, "Work.Approve", nameof(WorkSubmission), sub.Id);
        }
        _notifications.Add(employerId, NotificationType.JobCompleted, "Job completed",
            $"\"{job.Title}\" is complete. Please record the payment and leave a rating.", job.Id, a.Id, $"/Assignments/Details/{a.Id}");

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("Another review was saved at the same time. Please reload.");
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("A winner was already recorded for this challenge.");
        }
        await tx.CommitAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Standard mode: employer requests changes (reason required).</summary>
    public async Task<ServiceResult> RequestRevisionAsync(string employerId, int submissionId, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) return ServiceResult.Fail("Please explain what needs to be revised.");
        var sub = await LoadSubmissionAsync(submissionId);
        if (sub is null) return ServiceResult.Fail("Submission not found.");
        var a = sub.TaskAssignment;
        if (a.Job.EmployerId != employerId) return ServiceResult.Fail("You can only review work for your own jobs.");
        if (a.IsChallengeParticipant) return ServiceResult.Fail("Challenge submissions are approved or rejected, not revised.");
        if (sub.Status != SubmissionStatus.PendingReview || a.Status != AssignmentStatus.Submitted) return ServiceResult.Fail("This submission has already been reviewed.");

        sub.Status = SubmissionStatus.RevisionRequested;
        sub.EmployerFeedback = reason.Trim();
        sub.ReviewedAt = Now;
        a.Status = AssignmentStatus.RevisionRequested;
        a.Job.Status = JobStatus.InProgress;
        a.Job.UpdatedAt = Now;
        if (a.Payment != null) _payments.Transition(a.Payment, PaymentStatus.Pending, employerId, "Revision requested");
        _notifications.Add(a.HunterId, NotificationType.RevisionRequested, "Revision requested",
            $"The employer requested changes to \"{a.Job.Title}\": {reason.Trim()}", a.JobId, a.Id, $"/Assignments/Details/{a.Id}");
        _audit.Add(employerId, "Work.RequestRevision", nameof(WorkSubmission), sub.Id, reason);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Challenge mode: the earliest pending submission failed review; the participant may resubmit before the deadline.</summary>
    public async Task<ServiceResult> RejectChallengeSubmissionAsync(string employerId, int submissionId, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) return ServiceResult.Fail("Please give the reason the submission failed review.");
        var sub = await LoadSubmissionAsync(submissionId);
        if (sub is null) return ServiceResult.Fail("Submission not found.");
        var a = sub.TaskAssignment;
        var job = a.Job;
        if (job.EmployerId != employerId) return ServiceResult.Fail("Not authorized.");
        if (!a.IsChallengeParticipant) return ServiceResult.Fail("Only challenge submissions can be rejected this way.");
        if (job.WinnerAssignmentId != null) return ServiceResult.Fail("This challenge already has a winner.");
        if (sub.Status != SubmissionStatus.PendingReview) return ServiceResult.Fail("This submission has already been reviewed.");
        var all = job.Assignments.SelectMany(x => x.Submissions).ToList();
        if (ChallengeRules.NextReviewableSubmissionId(all) != sub.Id)
            return ServiceResult.Fail("Submissions must be reviewed in the order they were received.");

        sub.Status = SubmissionStatus.Rejected;
        sub.EmployerFeedback = reason.Trim();
        sub.ReviewedAt = Now;
        a.Status = AssignmentStatus.InProgress; // may resubmit before the deadline (new timestamp)
        _notifications.Add(a.HunterId, NotificationType.RevisionRequested, "Submission failed review",
            $"Your submission for \"{job.Title}\" did not pass review: {reason.Trim()}. You may resubmit before {job.ProjectDeadline.Local()}.",
            job.Id, a.Id, $"/Assignments/Details/{a.Id}");
        _audit.Add(employerId, "Challenge.RejectSubmission", nameof(WorkSubmission), sub.Id, reason);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Authorizes download of a submission file (assignment Hunter, job Employer, Admin).</summary>
    public async Task<WorkSubmission?> GetSubmissionForDownloadAsync(string userId, bool isAdmin, int submissionId)
    {
        var s = await _db.WorkSubmissions.AsNoTracking().Include(x => x.TaskAssignment).ThenInclude(a => a.Job)
            .FirstOrDefaultAsync(x => x.Id == submissionId);
        if (s is null) return null;
        return isAdmin || s.TaskAssignment.HunterId == userId || s.TaskAssignment.Job.EmployerId == userId ? s : null;
    }
}
