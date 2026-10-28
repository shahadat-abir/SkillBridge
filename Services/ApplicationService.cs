using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Domain;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.ViewModels;

namespace SkillBridge.Services;

/// <summary>Applications/bids, applicant comparison and Hunter selection (standard mode).</summary>
public class ApplicationService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly FileStorageService _files;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;
    private readonly ReputationService _reputation;

    public ApplicationService(ApplicationDbContext db, TimeProvider clock, FileStorageService files,
        NotificationService notifications, AuditService audit, ReputationService reputation)
    {
        _db = db;
        _clock = clock;
        _files = files;
        _notifications = notifications;
        _audit = audit;
        _reputation = reputation;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- Hunter side

    public async Task<ServiceResult<int>> ApplyAsync(string hunterId, ApplyFormModel f)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == f.JobId);
        if (job is null) return ServiceResult<int>.Fail("Job not found.");
        if (job.Mode != JobMode.Standard) return ServiceResult<int>.Fail("This is a challenge. Use 'Join challenge' instead.");
        if (job.EmployerId == hunterId) return ServiceResult<int>.Fail("You cannot apply to your own job.");
        if (!JobRules.IsAcceptingApplications(job, Now)) return ServiceResult<int>.Fail("This job is no longer accepting applications.");
        if (await _db.JobApplications.AnyAsync(a => a.JobId == job.Id && a.HunterId == hunterId))
            return ServiceResult<int>.Fail("You have already applied to this job.");
        if (f.ProposedAmount <= 0 || f.ProposedDays < 1) return ServiceResult<int>.Fail("Proposed price and time must be positive.");
        if (job.PaymentType == PaymentType.FixedPrice && f.ProposedAmount != job.Budget)
            return ServiceResult<int>.Fail($"This job has a fixed price of {job.Budget:N2} {job.Currency}. Your proposed price must equal the budget.");

        var app = new JobApplication
        {
            JobId = job.Id,
            HunterId = hunterId,
            CoverLetter = f.CoverLetter.Trim(),
            ProposedAmount = f.ProposedAmount,
            ProposedDays = f.ProposedDays,
            RelevantExperience = f.RelevantExperience?.Trim(),
            Status = ApplicationStatus.Submitted,
            AppliedAt = Now,
            UpdatedAt = Now
        };
        if (f.Attachment is { Length: > 0 })
        {
            var saved = await _files.SaveAsync(f.Attachment, "applications");
            if (!saved.Succeeded) return ServiceResult<int>.Fail(saved.Error!);
            app.AttachmentPath = saved.Value!.RelativePath;
            app.AttachmentOriginalName = saved.Value.OriginalName;
        }

        _db.JobApplications.Add(app);
        _notifications.Add(job.EmployerId, NotificationType.NewApplication, "New application received",
            $"A Hunter applied to \"{job.Title}\" proposing {f.ProposedAmount:N2} {job.Currency} in {f.ProposedDays} day(s).",
            job.Id, null, $"/Applications/ForJob/{job.Id}");
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult<int>.Fail("You have already applied to this job.");
        }
        _audit.Add(hunterId, "Application.Submit", nameof(JobApplication), app.Id, $"Job {job.Id}");
        await _db.SaveChangesAsync();
        return ServiceResult<int>.Ok(app.Id);
    }

    public async Task<ServiceResult> WithdrawAsync(string hunterId, int applicationId)
    {
        var app = await _db.JobApplications.Include(a => a.Job).FirstOrDefaultAsync(a => a.Id == applicationId);
        if (app is null || app.HunterId != hunterId) return ServiceResult.Fail("Application not found.");
        if (!ApplicationRules.IsActive(app.Status))
            return ServiceResult.Fail(app.Status == ApplicationStatus.Accepted
                ? "You were selected. Decline the assignment from its page instead."
                : "This application can no longer be withdrawn.");
        app.Status = ApplicationStatus.Withdrawn;
        app.UpdatedAt = Now;
        _notifications.Add(app.Job.EmployerId, NotificationType.General, "Application withdrawn",
            $"An applicant withdrew from \"{app.Job.Title}\".", app.JobId, null, $"/Applications/ForJob/{app.JobId}");
        _audit.Add(hunterId, "Application.Withdraw", nameof(JobApplication), app.Id);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public Task<List<JobApplication>> ListForHunterAsync(string hunterId) =>
        _db.JobApplications.AsNoTracking()
            .Include(a => a.Job).ThenInclude(j => j.Category)
            .Where(a => a.HunterId == hunterId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync();

    // ---------------------------------------------------------------- Employer side

    public async Task<ServiceResult<ApplicationsForJobViewModel>> ForJobAsync(string employerId, bool isAdmin, int jobId)
    {
        var job = await _db.Jobs.AsNoTracking().Include(j => j.Assignments).FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return ServiceResult<ApplicationsForJobViewModel>.Fail("Job not found.");
        if (!isAdmin && job.EmployerId != employerId) return ServiceResult<ApplicationsForJobViewModel>.Fail("You can only manage applications for your own jobs.");
        var apps = await _db.JobApplications.AsNoTracking().Include(a => a.Hunter)
            .Where(a => a.JobId == jobId).OrderBy(a => a.AppliedAt).ToListAsync();
        return ServiceResult<ApplicationsForJobViewModel>.Ok(new ApplicationsForJobViewModel
        {
            Job = job,
            Applications = apps,
            CanSelect = !isAdmin && CanSelectFor(job)
        });
    }

    private static bool CanSelectFor(Job job) =>
        job.Mode == JobMode.Standard
        && job.Status is JobStatus.Open or JobStatus.Closed
        && !job.Assignments.Any(a => a.Status is not (AssignmentStatus.Declined or AssignmentStatus.Cancelled));

    /// <summary>Employer-only status changes: UnderReview, Shortlisted, Rejected.</summary>
    public async Task<ServiceResult> ChangeStatusAsync(string employerId, int applicationId, ApplicationStatus to, string? note)
    {
        if (to is not (ApplicationStatus.UnderReview or ApplicationStatus.Shortlisted or ApplicationStatus.Rejected))
            return ServiceResult.Fail("Invalid status change.");
        var app = await _db.JobApplications.Include(a => a.Job).FirstOrDefaultAsync(a => a.Id == applicationId);
        if (app is null) return ServiceResult.Fail("Application not found.");
        if (app.Job.EmployerId != employerId) return ServiceResult.Fail("You can only manage applications for your own jobs.");
        if (app.Job.Status is JobStatus.Cancelled or JobStatus.Completed) return ServiceResult.Fail("This job is finished.");
        if (!ApplicationRules.CanTransition(app.Status, to)) return ServiceResult.Fail($"Cannot change a {app.Status} application to {to}.");

        app.Status = to;
        app.UpdatedAt = Now;
        if (!string.IsNullOrWhiteSpace(note)) app.EmployerNote = note.Trim();
        var (type, title) = to switch
        {
            ApplicationStatus.Shortlisted => (NotificationType.ApplicationShortlisted, "You were shortlisted"),
            ApplicationStatus.Rejected => (NotificationType.ApplicationRejected, "Application not successful"),
            _ => (NotificationType.General, "Application under review")
        };
        _notifications.Add(app.HunterId, type, title, $"Your application for \"{app.Job.Title}\" is now: {to}.", app.JobId, null, "/Applications/Mine");
        _audit.Add(employerId, $"Application.{to}", nameof(JobApplication), app.Id);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>
    /// Selects a Hunter for a standard job, atomically:
    /// validates ownership and eligibility, accepts the application, creates the assignment
    /// (awaiting the Hunter's acceptance), updates the job and notifies the Hunter.
    /// Conflicting simultaneous selections are prevented by the job's concurrency stamp
    /// and the unique index on assignments.
    /// </summary>
    public async Task<ServiceResult<int>> SelectAsync(string employerId, int applicationId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var app = await _db.JobApplications.Include(a => a.Job).ThenInclude(j => j.Assignments)
            .FirstOrDefaultAsync(a => a.Id == applicationId);
        if (app is null) return ServiceResult<int>.Fail("Application not found.");
        var job = app.Job;
        if (job.EmployerId != employerId) return ServiceResult<int>.Fail("You can only select Hunters for your own jobs.");
        if (job.Mode != JobMode.Standard) return ServiceResult<int>.Fail("Challenge jobs do not use selection.");
        if (!CanSelectFor(job)) return ServiceResult<int>.Fail("A Hunter has already been selected for this job, or the job is not open for selection.");
        if (!ApplicationRules.IsActive(app.Status)) return ServiceResult<int>.Fail($"A {app.Status} application cannot be selected.");

        app.Status = ApplicationStatus.Accepted;
        app.UpdatedAt = Now;
        var assignment = new TaskAssignment
        {
            JobId = job.Id,
            HunterId = app.HunterId,
            ApplicationId = app.Id,
            AgreedAmount = app.ProposedAmount,
            AssignedAt = Now,
            DueDate = job.ProjectDeadline,
            Status = AssignmentStatus.PendingAcceptance
        };
        _db.TaskAssignments.Add(assignment);
        job.Status = JobStatus.Assigned;
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<int>.Fail("Another selection was made at the same time. Please reload.");
        }
        catch (DbUpdateException)
        {
            return ServiceResult<int>.Fail("This Hunter or application is already assigned.");
        }

        _notifications.Add(app.HunterId, NotificationType.HunterSelected, "You have been selected!",
            $"The employer selected you for \"{job.Title}\" at {assignment.AgreedAmount:N2} {job.Currency}. Please accept or decline the assignment.",
            job.Id, assignment.Id, $"/Assignments/Details/{assignment.Id}");
        _audit.Add(employerId, "Hunter.Select", nameof(TaskAssignment), assignment.Id, $"Job {job.Id}, application {app.Id}");
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<int>.Ok(assignment.Id);
    }

    // ---------------------------------------------------------------- comparison

    public async Task<ServiceResult<ComparisonViewModel>> CompareAsync(string employerId, bool isAdmin, int jobId, string sort, IReadOnlyCollection<int>? onlyIds = null)
    {
        var job = await _db.Jobs.AsNoTracking()
            .Include(j => j.RequiredSkills).ThenInclude(s => s.Skill)
            .Include(j => j.Assignments)
            .Include(j => j.Category)
            .AsSplitQuery()
            .FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return ServiceResult<ComparisonViewModel>.Fail("Job not found.");
        if (!isAdmin && job.EmployerId != employerId) return ServiceResult<ComparisonViewModel>.Fail("You can only compare applicants for your own jobs.");

        var appsQuery = _db.JobApplications.AsNoTracking()
            .Include(a => a.Hunter).ThenInclude(h => h.Skills)
            .Where(a => a.JobId == jobId && a.Status != ApplicationStatus.Withdrawn);
        if (onlyIds is { Count: > 0 }) appsQuery = appsQuery.Where(a => onlyIds.Contains(a.Id));
        var apps = await appsQuery.AsSplitQuery().ToListAsync();

        var reps = await _reputation.ForUsersAsync(apps.Select(a => a.HunterId).ToList());
        var required = job.RequiredSkills.Select(s => new RequiredSkill(s.SkillId, s.Skill.Name, s.MinimumLevel)).ToList();

        var rows = apps.Select(a =>
        {
            var rep = reps[a.HunterId];
            return new ApplicantComparisonRow
            {
                ApplicationId = a.Id,
                HunterId = a.HunterId,
                Name = a.Hunter.FullName,
                Headline = a.Hunter.Headline,
                Status = a.Status,
                ProposedAmount = a.ProposedAmount,
                ProposedDays = a.ProposedDays,
                AppliedAt = a.AppliedAt,
                Match = SkillMatcher.Match(required, a.Hunter.Skills.Select(s => new HunterSkillLevel(s.SkillId, s.Level, s.YearsOfExperience))),
                AverageRating = rep.AverageRating,
                RatingCount = rep.RatingCount,
                CompletedJobs = rep.CompletedJobs,
                PortfolioUrl = a.Hunter.PortfolioUrl,
                Education = a.Hunter.Education,
                Experience = a.Hunter.Experience,
                RelevantExperience = a.RelevantExperience,
                Availability = a.Hunter.Availability,
                CoverLetter = a.CoverLetter,
                HasAttachment = a.AttachmentPath != null,
                WithinBudget = a.ProposedAmount <= job.Budget,
                WithinExpectedTime = a.ProposedDays <= job.ExpectedCompletionDays
            };
        }).ToList();

        // Highlight best values (informational only — the Employer decides).
        if (rows.Count > 0)
        {
            var minPrice = rows.Min(r => r.ProposedAmount);
            var minDays = rows.Min(r => r.ProposedDays);
            var bestMatch = rows.Max(r => r.Match.Percent);
            var bestRating = rows.Where(r => r.AverageRating.HasValue).Select(r => r.AverageRating!.Value).DefaultIfEmpty(-1).Max();
            foreach (var r in rows)
            {
                r.IsLowestPrice = r.ProposedAmount == minPrice;
                r.IsFastest = r.ProposedDays == minDays;
                r.IsBestSkillMatch = r.Match.Percent == bestMatch && bestMatch > 0;
                r.IsHighestRated = bestRating > 0 && r.AverageRating == bestRating;
            }
        }

        rows = sort switch
        {
            "price" => rows.OrderBy(r => r.ProposedAmount).ThenBy(r => r.AppliedAt).ToList(),
            "time" => rows.OrderBy(r => r.ProposedDays).ThenBy(r => r.AppliedAt).ToList(),
            "match" => rows.OrderByDescending(r => r.Match.Percent).ThenBy(r => r.AppliedAt).ToList(),
            "rating" => rows.OrderByDescending(r => r.AverageRating ?? -1).ThenByDescending(r => r.RatingCount).ToList(),
            "completed" => rows.OrderByDescending(r => r.CompletedJobs).ThenBy(r => r.AppliedAt).ToList(),
            _ => rows.OrderBy(r => r.AppliedAt).ToList()
        };

        return ServiceResult<ComparisonViewModel>.Ok(new ComparisonViewModel
        {
            Job = job,
            Rows = rows,
            Sort = sort,
            CanSelect = !isAdmin && CanSelectFor(job),
            HasActiveAssignment = job.Assignments.Any(a => a.Status is not (AssignmentStatus.Declined or AssignmentStatus.Cancelled)),
            SelectedIds = onlyIds?.ToList() ?? new List<int>()
        });
    }

    /// <summary>Authorizes download of an application attachment (owner Hunter, job Employer, or Admin).</summary>
    public async Task<JobApplication?> GetForDownloadAsync(string userId, bool isAdmin, int applicationId)
    {
        var app = await _db.JobApplications.AsNoTracking().Include(a => a.Job).FirstOrDefaultAsync(a => a.Id == applicationId);
        if (app is null) return null;
        return isAdmin || app.HunterId == userId || app.Job.EmployerId == userId ? app : null;
    }
}
