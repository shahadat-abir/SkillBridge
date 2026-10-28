using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.Domain;

public static class JobRules
{
    public const int MaxChallengeParticipants = 10;

    /// <summary>True when the job accepts new applications / challenge registrations.</summary>
    public static bool IsAcceptingApplications(Job job, DateTime utcNow) =>
        job.Status == JobStatus.Open && utcNow < job.ApplicationDeadline;

    /// <summary>Validates job input. Returns a list of (field, message) errors.</summary>
    public static List<(string Field, string Message)> Validate(
        decimal budget, int expectedDays, DateTime applicationDeadlineUtc, DateTime projectDeadlineUtc,
        JobMode mode, int maxParticipants, DateTime utcNow, bool isNew)
    {
        var errors = new List<(string, string)>();
        if (budget <= 0) errors.Add(("Budget", "Budget must be greater than zero."));
        if (expectedDays < 1 || expectedDays > 365) errors.Add(("ExpectedCompletionDays", "Expected completion time must be between 1 and 365 days."));
        if (isNew && applicationDeadlineUtc <= utcNow) errors.Add(("ApplicationDeadline", "Application deadline must be in the future."));
        if (projectDeadlineUtc <= applicationDeadlineUtc) errors.Add(("ProjectDeadline", "Project deadline must be after the application deadline."));
        if (mode == JobMode.Challenge && (maxParticipants < 2 || maxParticipants > MaxChallengeParticipants))
            errors.Add(("MaxParticipants", $"A challenge needs between 2 and {MaxChallengeParticipants} participants."));
        return errors;
    }

    /// <summary>Employer may edit only before anyone is selected/registered.</summary>
    public static bool CanEdit(Job job) => job.Status is JobStatus.Open or JobStatus.Closed;

    public static bool CanClose(Job job) => job.Status == JobStatus.Open;

    public static bool CanReopen(Job job, DateTime utcNow) => job.Status == JobStatus.Closed && utcNow < job.ApplicationDeadline;

    public static bool CanCancel(Job job) => job.Status is JobStatus.Open or JobStatus.Closed or JobStatus.Assigned;
}
