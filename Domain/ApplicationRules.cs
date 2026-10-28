using SkillBridge.Models.Enums;

namespace SkillBridge.Domain;

/// <summary>Allowed application status transitions (enforced in services, never by form input).</summary>
public static class ApplicationRules
{
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Submitted]   = new[] { ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted, ApplicationStatus.Accepted, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn },
        [ApplicationStatus.UnderReview] = new[] { ApplicationStatus.Shortlisted, ApplicationStatus.Accepted, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn },
        [ApplicationStatus.Shortlisted] = new[] { ApplicationStatus.Accepted, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn },
        [ApplicationStatus.Accepted]    = new[] { ApplicationStatus.Withdrawn },   // Hunter declines the assignment
        [ApplicationStatus.Rejected]    = Array.Empty<ApplicationStatus>(),
        [ApplicationStatus.Withdrawn]   = Array.Empty<ApplicationStatus>()
    };

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Applications that still compete for the job.</summary>
    public static bool IsActive(ApplicationStatus s) =>
        s is ApplicationStatus.Submitted or ApplicationStatus.UnderReview or ApplicationStatus.Shortlisted;
}
