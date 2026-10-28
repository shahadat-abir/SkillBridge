using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Models.Enums;
using SkillBridge.ViewModels;

namespace SkillBridge.Services;

/// <summary>All dashboard figures are computed from real records, scoped to the current user.</summary>
public class DashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly ReputationService _reputation;

    public DashboardService(ApplicationDbContext db, ReputationService reputation)
    {
        _db = db;
        _reputation = reputation;
    }

    public async Task<HunterDashboardViewModel> HunterAsync(string userId)
    {
        var apps = _db.JobApplications.AsNoTracking().Where(a => a.HunterId == userId);
        var asg = _db.TaskAssignments.AsNoTracking().Where(a => a.HunterId == userId);
        var pays = _db.Payments.AsNoTracking().Where(p => p.HunterId == userId);
        var rep = await _reputation.ForUserAsync(userId);

        return new HunterDashboardViewModel
        {
            TotalApplications = await apps.CountAsync(),
            PendingApplications = await apps.CountAsync(a => a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview || a.Status == ApplicationStatus.Shortlisted),
            AcceptedApplications = await apps.CountAsync(a => a.Status == ApplicationStatus.Accepted),
            AwaitingMyAcceptance = await asg.CountAsync(a => a.Status == AssignmentStatus.PendingAcceptance),
            ActiveAssignments = await asg.CountAsync(a => a.Status == AssignmentStatus.InProgress || a.Status == AssignmentStatus.RevisionRequested),
            AwaitingReview = await asg.CountAsync(a => a.Status == AssignmentStatus.Submitted),
            CompletedWork = rep.CompletedJobs,
            AverageRating = rep.AverageRating,
            RatingCount = rep.RatingCount,
            PaidTotal = await pays.Where(p => p.Status == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0,
            PendingPaymentTotal = await pays.Where(p => p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.AwaitingApproval || p.Status == PaymentStatus.Pending).SumAsync(p => (decimal?)p.Amount) ?? 0,
            SkillCount = await _db.UserSkills.CountAsync(s => s.UserId == userId),
            CurrentAssignments = await asg.Include(a => a.Job)
                .Where(a => a.Status == AssignmentStatus.PendingAcceptance || a.Status == AssignmentStatus.InProgress || a.Status == AssignmentStatus.Submitted || a.Status == AssignmentStatus.RevisionRequested)
                .OrderBy(a => a.DueDate).Take(10).ToListAsync(),
            RecentApplications = await apps.Include(a => a.Job).OrderByDescending(a => a.AppliedAt).Take(5).ToListAsync()
        };
    }

    public async Task<EmployerDashboardViewModel> EmployerAsync(string userId)
    {
        var jobs = _db.Jobs.AsNoTracking().Where(j => j.EmployerId == userId);
        var pays = _db.Payments.AsNoTracking().Where(p => p.EmployerId == userId);
        return new EmployerDashboardViewModel
        {
            ActiveJobs = await jobs.CountAsync(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled),
            ApplicationsReceived = await _db.JobApplications.CountAsync(a => a.Job.EmployerId == userId),
            JobsAwaitingSelection = await jobs.CountAsync(j => j.Mode == JobMode.Standard && (j.Status == JobStatus.Open || j.Status == JobStatus.Closed)
                && j.Applications.Any(a => a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview || a.Status == ApplicationStatus.Shortlisted)),
            AssignmentsInProgress = await _db.TaskAssignments.CountAsync(a => a.Job.EmployerId == userId && (a.Status == AssignmentStatus.InProgress || a.Status == AssignmentStatus.RevisionRequested || a.Status == AssignmentStatus.PendingAcceptance)),
            SubmissionsAwaitingReview = await _db.WorkSubmissions.CountAsync(s => s.TaskAssignment.Job.EmployerId == userId && s.Status == SubmissionStatus.PendingReview),
            CompletedJobs = await jobs.CountAsync(j => j.Status == JobStatus.Completed),
            PaymentsDue = await pays.Where(p => p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.Failed).SumAsync(p => (decimal?)p.Amount) ?? 0,
            PaymentsPaid = await pays.Where(p => p.Status == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0,
            RecentJobs = await jobs.Include(j => j.Applications).Include(j => j.Assignments).OrderByDescending(j => j.CreatedAt).Take(6).AsSplitQuery().ToListAsync(),
            PendingSubmissions = await _db.WorkSubmissions.AsNoTracking()
                .Include(s => s.TaskAssignment).ThenInclude(a => a.Job)
                .Include(s => s.TaskAssignment).ThenInclude(a => a.Hunter)
                .Where(s => s.TaskAssignment.Job.EmployerId == userId && s.Status == SubmissionStatus.PendingReview)
                .OrderBy(s => s.SubmittedAt).Take(10).ToListAsync()
        };
    }
}
