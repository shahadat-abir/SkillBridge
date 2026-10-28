using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Domain;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.ViewModels;

namespace SkillBridge.Services;

public class JobService
{
    public const int PageSize = 10;

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly FileStorageService _files;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;
    private readonly PaymentService _payments;

    public JobService(ApplicationDbContext db, TimeProvider clock, FileStorageService files,
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

    // ---------------------------------------------------------------- search

    public async Task<PagedResult<JobListItemViewModel>> SearchAsync(JobSearchQuery q)
    {
        var now = Now;
        var jobs = _db.Jobs.AsNoTracking().AsQueryable();

        if (q.Status.HasValue) jobs = jobs.Where(j => j.Status == q.Status.Value);
        else if (!q.AllStatuses) jobs = jobs.Where(j => j.Status == JobStatus.Open && j.ApplicationDeadline > now);

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var k = q.Keyword.Trim();
            jobs = jobs.Where(j => j.Title.Contains(k) || j.Description.Contains(k) || j.RequiredSkills.Any(s => s.Skill.Name.Contains(k)));
        }
        if (q.CategoryId.HasValue) jobs = jobs.Where(j => j.CategoryId == q.CategoryId.Value);
        if (q.SkillId.HasValue) jobs = jobs.Where(j => j.RequiredSkills.Any(s => s.SkillId == q.SkillId.Value));
        if (q.Difficulty.HasValue) jobs = jobs.Where(j => j.Difficulty == q.Difficulty.Value);
        if (q.Mode.HasValue) jobs = jobs.Where(j => j.Mode == q.Mode.Value);
        if (q.MinBudget.HasValue) jobs = jobs.Where(j => j.Budget >= q.MinBudget.Value);
        if (q.MaxBudget.HasValue) jobs = jobs.Where(j => j.Budget <= q.MaxBudget.Value);

        jobs = q.Sort switch
        {
            "deadline" => jobs.OrderBy(j => j.ApplicationDeadline),
            "budget_desc" => jobs.OrderByDescending(j => j.Budget),
            "budget_asc" => jobs.OrderBy(j => j.Budget),
            "difficulty" => jobs.OrderBy(j => j.Difficulty).ThenByDescending(j => j.CreatedAt),
            _ => jobs.OrderByDescending(j => j.CreatedAt)
        };

        var page = Math.Max(1, q.Page);
        var total = await jobs.CountAsync();
        var items = await jobs.Skip((page - 1) * PageSize).Take(PageSize)
            .Select(j => new JobListItemViewModel
            {
                Id = j.Id,
                Title = j.Title,
                Summary = j.Description.Length > 220 ? j.Description.Substring(0, 220) + "…" : j.Description,
                CategoryName = j.Category.Name,
                Difficulty = j.Difficulty,
                Mode = j.Mode,
                Budget = j.Budget,
                Currency = j.Currency,
                PaymentType = j.PaymentType,
                ApplicationDeadline = j.ApplicationDeadline,
                ProjectDeadline = j.ProjectDeadline,
                Status = j.Status,
                Skills = j.RequiredSkills.Select(s => s.Skill.Name).ToList(),
                ApplicationsCount = j.Mode == JobMode.Challenge ? j.Assignments.Count() : j.Applications.Count(),
                MaxParticipants = j.MaxParticipants,
                EmployerName = j.Employer.CompanyName ?? j.Employer.FullName,
                CreatedAt = j.CreatedAt,
                IsAccepting = j.Status == JobStatus.Open && j.ApplicationDeadline > now,
                IsDemoData = j.IsDemoData
            })
            .ToListAsync();

        return new PagedResult<JobListItemViewModel> { Items = items, Page = page, PageSize = PageSize, TotalCount = total };
    }

    public Task<Job?> GetWithDetailsAsync(int id) =>
        _db.Jobs
            .Include(j => j.Category)
            .Include(j => j.Employer)
            .Include(j => j.RequiredSkills).ThenInclude(s => s.Skill)
            .Include(j => j.Assignments).ThenInclude(a => a.Hunter)
            .AsSplitQuery()
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<List<Job>> ListForEmployerAsync(string employerId) =>
        await _db.Jobs.AsNoTracking()
            .Include(j => j.Category)
            .Include(j => j.Applications)
            .Include(j => j.Assignments)
            .Where(j => j.EmployerId == employerId)
            .OrderByDescending(j => j.CreatedAt)
            .AsSplitQuery()
            .ToListAsync();

    // ---------------------------------------------------------------- create / edit

    public async Task<List<RequiredSkillInput>> BuildSkillInputsAsync(IEnumerable<JobSkill>? existing = null, IEnumerable<RequiredSkillInput>? posted = null)
    {
        var map = existing?.ToDictionary(e => e.SkillId, e => e.MinimumLevel) ?? new Dictionary<int, SkillLevel>();
        if (posted != null)
            map = posted.Where(p => p.Selected).GroupBy(p => p.SkillId).ToDictionary(g => g.Key, g => g.First().MinimumLevel);
        var skills = await _db.Skills.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return skills
            .Where(s => s.IsActive || map.ContainsKey(s.Id))
            .Select(s => new RequiredSkillInput
            {
                SkillId = s.Id,
                Name = s.Name,
                Selected = map.ContainsKey(s.Id),
                MinimumLevel = map.TryGetValue(s.Id, out var lvl) ? lvl : SkillLevel.Intermediate
            })
            .ToList();
    }

    public List<(string Field, string Message)> ValidateForm(JobFormModel f, bool isNew)
    {
        var appUtc = TimeDisplay.LocalInputToUtc(f.ApplicationDeadline);
        var projUtc = TimeDisplay.LocalInputToUtc(f.ProjectDeadline);
        var errors = JobRules.Validate(f.Budget, f.ExpectedCompletionDays, appUtc, projUtc, f.Mode, f.MaxParticipants, Now, isNew);
        if (!f.RequiredSkills.Any(s => s.Selected)) errors.Add(("RequiredSkills", "Select at least one required skill."));
        return errors;
    }

    public async Task<ServiceResult<int>> CreateAsync(string employerId, JobFormModel f)
    {
        if (!await _db.JobCategories.AnyAsync(c => c.Id == f.CategoryId && c.IsActive))
            return ServiceResult<int>.Fail("Please choose a valid category.");

        var job = new Job { EmployerId = employerId, CreatedAt = Now };
        Map(f, job);

        if (f.Attachment is { Length: > 0 })
        {
            var saved = await _files.SaveAsync(f.Attachment, "jobs");
            if (!saved.Succeeded) return ServiceResult<int>.Fail(saved.Error!);
            job.AttachmentPath = saved.Value!.RelativePath;
            job.AttachmentOriginalName = saved.Value.OriginalName;
        }

        await SetSkillsAsync(job, f.RequiredSkills);
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();
        _audit.Add(employerId, "Job.Create", nameof(Job), job.Id, job.Title);
        await _db.SaveChangesAsync();
        return ServiceResult<int>.Ok(job.Id);
    }

    public async Task<ServiceResult> UpdateAsync(string employerId, int jobId, JobFormModel f)
    {
        var job = await _db.Jobs.Include(j => j.RequiredSkills).Include(j => j.Applications).Include(j => j.Assignments)
            .FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return ServiceResult.Fail("Job not found.");
        if (job.EmployerId != employerId) return ServiceResult.Fail("You can only edit your own jobs.");
        if (!JobRules.CanEdit(job)) return ServiceResult.Fail("This job can no longer be edited.");
        if (job.Mode != f.Mode && (job.Applications.Count > 0 || job.Assignments.Count > 0))
            return ServiceResult.Fail("The job mode cannot be changed after applications or registrations were received.");
        if (f.Mode == JobMode.Challenge && f.MaxParticipants < job.Assignments.Count)
            return ServiceResult.Fail("Participant limit cannot be lower than the number already registered.");
        if (!await _db.JobCategories.AnyAsync(c => c.Id == f.CategoryId))
            return ServiceResult.Fail("Please choose a valid category.");

        Map(f, job);

        if (f.RemoveAttachment && job.AttachmentPath != null)
        {
            _files.Delete(job.AttachmentPath);
            job.AttachmentPath = null;
            job.AttachmentOriginalName = null;
        }
        if (f.Attachment is { Length: > 0 })
        {
            var saved = await _files.SaveAsync(f.Attachment, "jobs");
            if (!saved.Succeeded) return ServiceResult.Fail(saved.Error!);
            _files.Delete(job.AttachmentPath);
            job.AttachmentPath = saved.Value!.RelativePath;
            job.AttachmentOriginalName = saved.Value.OriginalName;
        }

        _db.JobSkills.RemoveRange(job.RequiredSkills);
        job.RequiredSkills.Clear();
        await SetSkillsAsync(job, f.RequiredSkills);
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();
        _audit.Add(employerId, "Job.Update", nameof(Job), job.Id, job.Title);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("The job was changed by another action. Please reload and try again.");
        }
        return ServiceResult.Ok();
    }

    private void Map(JobFormModel f, Job job)
    {
        job.Title = f.Title.Trim();
        job.Description = f.Description.Trim();
        job.CategoryId = f.CategoryId;
        job.Difficulty = f.Difficulty;
        job.Mode = f.Mode;
        job.Budget = f.Budget;
        job.PaymentType = f.Mode == JobMode.Challenge ? PaymentType.FixedPrice : f.PaymentType;
        job.ExpectedCompletionDays = f.ExpectedCompletionDays;
        job.ApplicationDeadline = TimeDisplay.LocalInputToUtc(f.ApplicationDeadline);
        job.ProjectDeadline = TimeDisplay.LocalInputToUtc(f.ProjectDeadline);
        job.MaxParticipants = f.Mode == JobMode.Challenge ? f.MaxParticipants : 1;
        job.EligibilityCriteria = f.EligibilityCriteria?.Trim();
        job.SubmissionInstructions = f.SubmissionInstructions?.Trim();
        job.Terms = f.Terms?.Trim();
        job.UpdatedAt = Now;
    }

    private async Task SetSkillsAsync(Job job, IEnumerable<RequiredSkillInput> inputs)
    {
        var selected = inputs.Where(s => s.Selected).Select(s => s.SkillId).Distinct().ToList();
        var valid = await _db.Skills.Where(s => selected.Contains(s.Id)).Select(s => s.Id).ToListAsync();
        foreach (var input in inputs.Where(s => s.Selected && valid.Contains(s.SkillId)).GroupBy(s => s.SkillId).Select(g => g.First()))
            job.RequiredSkills.Add(new JobSkill { SkillId = input.SkillId, MinimumLevel = input.MinimumLevel });
    }

    public static JobFormModel ToForm(Job job) => new()
    {
        Id = job.Id,
        Title = job.Title,
        Description = job.Description,
        CategoryId = job.CategoryId,
        Difficulty = job.Difficulty,
        Mode = job.Mode,
        Budget = job.Budget,
        PaymentType = job.PaymentType,
        ExpectedCompletionDays = job.ExpectedCompletionDays,
        ApplicationDeadline = TimeDisplay.UtcToLocalInput(job.ApplicationDeadline),
        ProjectDeadline = TimeDisplay.UtcToLocalInput(job.ProjectDeadline),
        MaxParticipants = job.Mode == JobMode.Challenge ? job.MaxParticipants : 3,
        EligibilityCriteria = job.EligibilityCriteria,
        SubmissionInstructions = job.SubmissionInstructions,
        Terms = job.Terms,
        ExistingAttachmentName = job.AttachmentOriginalName
    };

    // ---------------------------------------------------------------- status changes

    public async Task<ServiceResult> CloseAsync(string userId, bool isAdmin, int jobId)
    {
        var job = await _db.Jobs.Include(j => j.Assignments).FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return ServiceResult.Fail("Job not found.");
        if (!isAdmin && job.EmployerId != userId) return ServiceResult.Fail("Not authorized.");
        if (!JobRules.CanClose(job)) return ServiceResult.Fail("Only open jobs can be closed.");

        // Challenge with registered participants: closing registration starts the work phase.
        job.Status = job.Mode == JobMode.Challenge && job.Assignments.Any(a => a.Status == AssignmentStatus.InProgress)
            ? JobStatus.InProgress
            : JobStatus.Closed;
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();
        _audit.Add(userId, "Job.Close", nameof(Job), job.Id);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReopenAsync(string userId, int jobId)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return ServiceResult.Fail("Job not found.");
        if (job.EmployerId != userId) return ServiceResult.Fail("Not authorized.");
        if (!JobRules.CanReopen(job, Now)) return ServiceResult.Fail("Only closed jobs whose application deadline has not passed can be reopened.");
        job.Status = JobStatus.Open;
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();
        _audit.Add(userId, "Job.Reopen", nameof(Job), job.Id);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>
    /// Employer may cancel before work has started. Admin may cancel at any non-final stage (moderation).
    /// Active applications are rejected, active assignments cancelled and ledger entries cancelled.
    /// </summary>
    public async Task<ServiceResult> CancelAsync(string userId, bool isAdmin, int jobId, string? reason)
    {
        var job = await _db.Jobs
            .Include(j => j.Applications)
            .Include(j => j.Assignments).ThenInclude(a => a.Payment)
            .AsSplitQuery()
            .FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return ServiceResult.Fail("Job not found.");
        if (!isAdmin && job.EmployerId != userId) return ServiceResult.Fail("Not authorized.");
        if (job.Status is JobStatus.Completed or JobStatus.Cancelled) return ServiceResult.Fail("This job is already finished.");
        var challengeWorkStarted = job.Mode == JobMode.Challenge && job.Assignments.Any();
        if (!isAdmin && (!JobRules.CanCancel(job) || challengeWorkStarted))
            return ServiceResult.Fail("Work has already started. Contact an administrator to cancel this job.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        job.Status = JobStatus.Cancelled;
        job.UpdatedAt = Now;
        job.ConcurrencyStamp = Guid.NewGuid();

        foreach (var app in job.Applications.Where(a => ApplicationRules.IsActive(a.Status) || a.Status == ApplicationStatus.Accepted))
        {
            app.Status = ApplicationStatus.Rejected;
            app.UpdatedAt = Now;
            _notifications.Add(app.HunterId, NotificationType.ApplicationRejected, "Job cancelled",
                $"The job \"{job.Title}\" was cancelled.", job.Id, null, $"/Jobs/Details/{job.Id}");
        }
        foreach (var a in job.Assignments.Where(a => a.Status is not (AssignmentStatus.Completed or AssignmentStatus.Declined or AssignmentStatus.Cancelled or AssignmentStatus.ChallengeLost)))
        {
            a.Status = AssignmentStatus.Cancelled;
            if (a.Payment != null) _payments.Transition(a.Payment, PaymentStatus.Cancelled, userId, "Job cancelled");
            _notifications.Add(a.HunterId, NotificationType.General, "Assignment cancelled",
                $"Your assignment for \"{job.Title}\" was cancelled.", job.Id, a.Id, $"/Assignments/Details/{a.Id}");
        }
        if (isAdmin && job.EmployerId != userId)
            _notifications.Add(job.EmployerId, NotificationType.General, "Job cancelled by administrator",
                $"Your job \"{job.Title}\" was cancelled by an administrator. Reason: {reason ?? "not given"}", job.Id, null, $"/Jobs/Details/{job.Id}");

        _audit.Add(userId, isAdmin ? "Admin.Job.Cancel" : "Job.Cancel", nameof(Job), job.Id, reason);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult.Ok();
    }

    // ---------------------------------------------------------------- reports

    public async Task<ServiceResult> ReportAsync(string userId, int jobId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10) return ServiceResult.Fail("Please describe the problem (at least 10 characters).");
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return ServiceResult.Fail("Job not found.");
        if (await _db.ContentReports.AnyAsync(r => r.JobId == jobId && r.ReporterId == userId && r.Status == ReportStatus.Open))
            return ServiceResult.Fail("You already have an open report for this job.");
        _db.ContentReports.Add(new ContentReport { JobId = jobId, ReporterId = userId, Reason = reason.Trim(), CreatedAt = Now });
        _audit.Add(userId, "Job.Report", nameof(Job), jobId);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
