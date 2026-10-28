using SkillBridge.Domain;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.Services;

namespace SkillBridge.Tests;

public class JobRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Open_job_before_deadline_accepts_applications()
    {
        var job = new Job { Status = JobStatus.Open, ApplicationDeadline = Now.AddHours(1) };
        Assert.True(JobRules.IsAcceptingApplications(job, Now));
    }

    [Fact]
    public void Job_after_application_deadline_rejects_applications()
    {
        var job = new Job { Status = JobStatus.Open, ApplicationDeadline = Now.AddSeconds(-1) };
        Assert.False(JobRules.IsAcceptingApplications(job, Now));
    }

    [Theory]
    [InlineData(JobStatus.Closed)]
    [InlineData(JobStatus.Assigned)]
    [InlineData(JobStatus.Cancelled)]
    [InlineData(JobStatus.Completed)]
    public void Non_open_job_rejects_applications(JobStatus status)
    {
        var job = new Job { Status = status, ApplicationDeadline = Now.AddDays(3) };
        Assert.False(JobRules.IsAcceptingApplications(job, Now));
    }

    [Fact]
    public void Project_deadline_must_be_after_application_deadline()
    {
        var errors = JobRules.Validate(1000, 5, Now.AddDays(5), Now.AddDays(4), JobMode.Standard, 1, Now, isNew: true);
        Assert.Contains(errors, e => e.Field == "ProjectDeadline");
    }

    [Fact]
    public void New_job_application_deadline_must_be_in_future()
    {
        var errors = JobRules.Validate(1000, 5, Now.AddDays(-1), Now.AddDays(4), JobMode.Standard, 1, Now, isNew: true);
        Assert.Contains(errors, e => e.Field == "ApplicationDeadline");
    }

    [Fact]
    public void Budget_must_be_positive()
    {
        var errors = JobRules.Validate(0, 5, Now.AddDays(1), Now.AddDays(4), JobMode.Standard, 1, Now, isNew: true);
        Assert.Contains(errors, e => e.Field == "Budget");
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    [InlineData(JobRules.MaxChallengeParticipants + 1, false)]
    public void Challenge_participant_count_is_validated(int participants, bool valid)
    {
        var errors = JobRules.Validate(1000, 5, Now.AddDays(1), Now.AddDays(4), JobMode.Challenge, participants, Now, isNew: true);
        Assert.Equal(valid, !errors.Any(e => e.Field == "MaxParticipants"));
    }

    [Fact]
    public void Valid_job_has_no_errors()
    {
        Assert.Empty(JobRules.Validate(1000, 5, Now.AddDays(1), Now.AddDays(4), JobMode.Standard, 1, Now, isNew: true));
    }
}

public class ApplicationRulesTests
{
    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Shortlisted, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.UnderReview, true)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.Accepted, true)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Accepted, false)]
    [InlineData(ApplicationStatus.Withdrawn, ApplicationStatus.Submitted, false)]
    [InlineData(ApplicationStatus.Accepted, ApplicationStatus.Rejected, false)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.Submitted, false)]
    public void Transitions_follow_the_lifecycle(ApplicationStatus from, ApplicationStatus to, bool allowed) =>
        Assert.Equal(allowed, ApplicationRules.CanTransition(from, to));
}

public class PaymentRulesTests
{
    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.AwaitingApproval, true)]
    [InlineData(PaymentStatus.AwaitingApproval, PaymentStatus.Approved, true)]
    [InlineData(PaymentStatus.Approved, PaymentStatus.Paid, true)]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Paid, false)]          // cannot pay before approval
    [InlineData(PaymentStatus.AwaitingApproval, PaymentStatus.Paid, false)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Refunded, true)]
    [InlineData(PaymentStatus.Paid, PaymentStatus.Pending, false)]
    [InlineData(PaymentStatus.Cancelled, PaymentStatus.Approved, false)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Approved, true)]
    public void Transitions_follow_the_ledger_rules(PaymentStatus from, PaymentStatus to, bool allowed) =>
        Assert.Equal(allowed, PaymentRules.CanTransition(from, to));
}

public class SkillMatcherTests
{
    [Fact]
    public void Match_reports_met_partial_and_missing_skills()
    {
        var required = new[]
        {
            new RequiredSkill(1, "C#", SkillLevel.Intermediate),
            new RequiredSkill(2, "SQL", SkillLevel.Advanced),
            new RequiredSkill(3, "React", SkillLevel.Beginner)
        };
        var hunter = new[]
        {
            new HunterSkillLevel(1, SkillLevel.Expert, 4),
            new HunterSkillLevel(2, SkillLevel.Beginner, 1)
        };

        var result = SkillMatcher.Match(required, hunter);

        Assert.Equal(3, result.Required);
        Assert.Equal(1, result.Met);
        Assert.Equal(1, result.Partial);
        Assert.Equal(33, result.Percent);
        Assert.True(result.Lines.Single(l => l.SkillId == 1).MeetsLevel);
        Assert.False(result.Lines.Single(l => l.SkillId == 2).MeetsLevel);
        Assert.False(result.Lines.Single(l => l.SkillId == 3).HasSkill);
    }

    [Fact]
    public void Job_without_required_skills_is_100_percent() =>
        Assert.Equal(100, SkillMatcher.Match(Array.Empty<RequiredSkill>(), Array.Empty<HunterSkillLevel>()).Percent);
}

public class ChallengeRulesTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Earliest_pending_submission_is_next_even_by_milliseconds()
    {
        var subs = new List<WorkSubmission>
        {
            new() { Id = 10, SubmittedAt = T0.AddMilliseconds(5), Status = SubmissionStatus.PendingReview },
            new() { Id = 11, SubmittedAt = T0.AddMilliseconds(1), Status = SubmissionStatus.PendingReview },
            new() { Id = 12, SubmittedAt = T0, Status = SubmissionStatus.Rejected }
        };
        Assert.Equal(11, ChallengeRules.NextReviewableSubmissionId(subs));
    }

    [Fact]
    public void Identical_timestamps_are_ordered_by_id()
    {
        var subs = new List<WorkSubmission>
        {
            new() { Id = 21, SubmittedAt = T0, Status = SubmissionStatus.PendingReview },
            new() { Id = 20, SubmittedAt = T0, Status = SubmissionStatus.PendingReview }
        };
        Assert.Equal(20, ChallengeRules.NextReviewableSubmissionId(subs));
    }

    [Fact]
    public void Cannot_join_full_challenge()
    {
        var job = new Job { Mode = JobMode.Challenge, Status = JobStatus.Open, MaxParticipants = 3, ApplicationDeadline = T0.AddDays(1) };
        Assert.False(ChallengeRules.CanJoin(job, 3, T0, out var reason));
        Assert.NotNull(reason);
        Assert.True(ChallengeRules.CanJoin(job, 2, T0, out _));
    }
}

public class FileSignatureTests
{
    [Fact]
    public void Pdf_signature_is_recognised() =>
        Assert.True(FileStorageService.SignatureMatches(".pdf", "%PDF-1.7"u8));

    [Fact]
    public void Executable_renamed_to_pdf_is_rejected() =>
        Assert.False(FileStorageService.SignatureMatches(".pdf", new byte[] { 0x4D, 0x5A, 0x90, 0x00 }));

    [Fact]
    public void Unknown_extension_is_rejected() =>
        Assert.False(FileStorageService.SignatureMatches(".exe", new byte[] { 0x4D, 0x5A }));
}
