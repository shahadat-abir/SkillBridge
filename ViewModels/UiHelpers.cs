using System.ComponentModel.DataAnnotations;
using System.Reflection;
using SkillBridge.Models.Enums;

namespace SkillBridge.ViewModels;

public class PagerModel
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string Action { get; set; } = "Index";
    public string? Controller { get; set; }
    public Dictionary<string, string?> RouteValues { get; set; } = new();
}

public static class UiHelpers
{
    public static string DisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    public static string StarsText(this JobDifficulty d) => new string('★', (int)d) + new string('☆', 3 - (int)d);

    public static string Badge(JobStatus s) => s switch
    {
        JobStatus.Open => "text-bg-success",
        JobStatus.Closed => "text-bg-secondary",
        JobStatus.Assigned => "text-bg-info",
        JobStatus.InProgress => "text-bg-primary",
        JobStatus.UnderReview => "text-bg-warning",
        JobStatus.Completed => "text-bg-dark",
        _ => "text-bg-danger"
    };

    public static string Badge(ApplicationStatus s) => s switch
    {
        ApplicationStatus.Submitted => "text-bg-secondary",
        ApplicationStatus.UnderReview => "text-bg-info",
        ApplicationStatus.Shortlisted => "text-bg-primary",
        ApplicationStatus.Accepted => "text-bg-success",
        ApplicationStatus.Rejected => "text-bg-danger",
        _ => "text-bg-light border"
    };

    public static string Badge(AssignmentStatus s) => s switch
    {
        AssignmentStatus.PendingAcceptance => "text-bg-info",
        AssignmentStatus.InProgress => "text-bg-primary",
        AssignmentStatus.Submitted => "text-bg-warning",
        AssignmentStatus.RevisionRequested => "text-bg-warning",
        AssignmentStatus.Completed => "text-bg-success",
        _ => "text-bg-secondary"
    };

    public static string Badge(SubmissionStatus s) => s switch
    {
        SubmissionStatus.PendingReview => "text-bg-warning",
        SubmissionStatus.Approved => "text-bg-success",
        SubmissionStatus.RevisionRequested => "text-bg-info",
        SubmissionStatus.Rejected => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string Badge(PaymentStatus s) => s switch
    {
        PaymentStatus.Pending => "text-bg-secondary",
        PaymentStatus.AwaitingApproval => "text-bg-info",
        PaymentStatus.Approved => "text-bg-warning",
        PaymentStatus.Paid => "text-bg-success",
        PaymentStatus.Failed => "text-bg-danger",
        _ => "text-bg-dark"
    };

    public static string Money(decimal amount, string currency = "BDT") => $"{amount:N2} {currency}";
}
