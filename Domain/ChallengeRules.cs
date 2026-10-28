using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.Domain;

/// <summary>
/// Challenge ("first valid submission wins") rules — documented interpretation:
///  * Up to Job.MaxParticipants Hunters register before the application deadline.
///  * Each participant may submit before the project deadline; the server timestamp is recorded.
///  * The Employer reviews submissions strictly in server-timestamp order. The earliest pending
///    submission is the only one that can be approved. If it fails review (rejected), the next earliest
///    becomes reviewable.
///  * The first submission that passes review wins; the winner is set once under concurrency control,
///    and all other participants are closed. Only the winner's payment proceeds.
/// </summary>
public static class ChallengeRules
{
    /// <summary>Returns the id of the only submission the Employer may currently decide on, or null.</summary>
    public static int? NextReviewableSubmissionId(IEnumerable<WorkSubmission> allChallengeSubmissions) =>
        allChallengeSubmissions
            .Where(s => s.Status == SubmissionStatus.PendingReview)
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Select(s => (int?)s.Id)
            .FirstOrDefault();

    public static bool CanJoin(Job job, int activeParticipants, DateTime utcNow, out string? reason)
    {
        reason = null;
        if (job.Mode != JobMode.Challenge) reason = "This job is not a challenge.";
        else if (!JobRules.IsAcceptingApplications(job, utcNow)) reason = "Registration for this challenge is closed.";
        else if (activeParticipants >= job.MaxParticipants) reason = "All participant places are already taken.";
        return reason is null;
    }
}
