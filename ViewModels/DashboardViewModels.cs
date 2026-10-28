using SkillBridge.Models;

namespace SkillBridge.ViewModels;

public class HunterDashboardViewModel
{
    public int TotalApplications { get; set; }
    public int PendingApplications { get; set; }
    public int AcceptedApplications { get; set; }
    public int AwaitingMyAcceptance { get; set; }
    public int ActiveAssignments { get; set; }
    public int AwaitingReview { get; set; }
    public int CompletedWork { get; set; }
    public double? AverageRating { get; set; }
    public int RatingCount { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal PendingPaymentTotal { get; set; }
    public int SkillCount { get; set; }
    public List<TaskAssignment> CurrentAssignments { get; set; } = new();
    public List<JobApplication> RecentApplications { get; set; } = new();
}

public class EmployerDashboardViewModel
{
    public int ActiveJobs { get; set; }
    public int ApplicationsReceived { get; set; }
    public int JobsAwaitingSelection { get; set; }
    public int AssignmentsInProgress { get; set; }
    public int SubmissionsAwaitingReview { get; set; }
    public int CompletedJobs { get; set; }
    public decimal PaymentsDue { get; set; }
    public decimal PaymentsPaid { get; set; }
    public List<Job> RecentJobs { get; set; } = new();
    public List<WorkSubmission> PendingSubmissions { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int Employers { get; set; }
    public int Hunters { get; set; }
    public int SuspendedUsers { get; set; }
    public int TotalJobs { get; set; }
    public int OpenJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int TotalApplications { get; set; }
    public int OpenReports { get; set; }
    public decimal LedgerPaid { get; set; }
    public decimal LedgerDue { get; set; }
    public List<AuditLog> RecentAudit { get; set; } = new();
}

public class AdminUserRow
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsSuspended { get; set; }
    public bool IsDemo { get; set; }
    public DateTime CreatedAt { get; set; }
}
