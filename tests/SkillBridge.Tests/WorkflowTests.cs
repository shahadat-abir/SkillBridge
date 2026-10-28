using Microsoft.EntityFrameworkCore;
using SkillBridge.Models.Enums;

namespace SkillBridge.Tests;

/// <summary>End-to-end business workflow tests over the real services (EF Core in-memory).</summary>
public class ApplicationWorkflowTests
{
    [Fact]
    public async Task Hunter_can_apply_once_and_employer_is_notified()
    {
        using var h = new TestHarness();
        var employer = h.AddUser("emp");
        var hunter = h.AddUser("hunter");
        var job = h.AddJob(employer);

        var first = await h.ApplyAsync(hunter, job);
        var second = await h.ApplyAsync(hunter, job);

        Assert.True(first.Succeeded, first.Error);
        Assert.False(second.Succeeded);
        Assert.Equal(1, await h.Db.JobApplications.CountAsync());
        Assert.True(await h.Db.Notifications.AnyAsync(n => n.UserId == employer.Id && n.Type == NotificationType.NewApplication));
    }

    [Fact]
    public async Task Cannot_apply_after_application_deadline()
    {
        using var h = new TestHarness();
        var job = h.AddJob(h.AddUser("emp"));
        h.Clock.Advance(TimeSpan.FromDays(6));
        var r = await h.ApplyAsync(h.AddUser("hunter"), job);
        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Employer_cannot_apply_to_own_job()
    {
        using var h = new TestHarness();
        var employer = h.AddUser("emp");
        var r = await h.ApplyAsync(employer, h.AddJob(employer));
        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Fixed_price_job_requires_budget_as_price()
    {
        using var h = new TestHarness();
        var job = h.AddJob(h.AddUser("emp"), paymentType: PaymentType.FixedPrice, budget: 5000);
        var hunter = h.AddUser("hunter");
        Assert.False((await h.ApplyAsync(hunter, job, amount: 4000)).Succeeded);
        Assert.True((await h.ApplyAsync(hunter, job, amount: 5000)).Succeeded);
    }

    [Fact]
    public async Task Only_job_owner_can_select_and_only_one_hunter_is_selected()
    {
        using var h = new TestHarness();
        var employer = h.AddUser("emp");
        var other = h.AddUser("other-emp");
        var h1 = h.AddUser("h1");
        var h2 = h.AddUser("h2");
        var job = h.AddJob(employer);
        var a1 = (await h.ApplyAsync(h1, job)).Value;
        var a2 = (await h.ApplyAsync(h2, job)).Value;

        Assert.False((await h.Applications.SelectAsync(other.Id, a1)).Succeeded);   // not the owner

        var selected = await h.Applications.SelectAsync(employer.Id, a1);
        Assert.True(selected.Succeeded, selected.Error);
        Assert.False((await h.Applications.SelectAsync(employer.Id, a2)).Succeeded); // conflicting selection

        var dbJob = await h.Db.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.Assigned, dbJob!.Status);
        Assert.Equal(ApplicationStatus.Accepted, (await h.Db.JobApplications.FindAsync(a1))!.Status);
        Assert.True(await h.Db.Notifications.AnyAsync(n => n.UserId == h1.Id && n.Type == NotificationType.HunterSelected));
    }

    [Fact]
    public async Task Comparison_uses_real_skill_and_rating_data()
    {
        using var h = new TestHarness();
        var employer = h.AddUser("emp");
        var strong = h.AddUser("strong");
        var weak = h.AddUser("weak");
        h.Db.UserSkills.Add(new Models.UserSkill { UserId = strong.Id, SkillId = 1, Level = SkillLevel.Advanced });
        h.Db.UserSkills.Add(new Models.UserSkill { UserId = strong.Id, SkillId = 3, Level = SkillLevel.Intermediate });
        h.Db.UserSkills.Add(new Models.UserSkill { UserId = weak.Id, SkillId = 1, Level = SkillLevel.Beginner });
        await h.Db.SaveChangesAsync();
        var job = h.AddJob(employer);
        await h.ApplyAsync(strong, job, amount: 9500, days: 6);
        await h.ApplyAsync(weak, job, amount: 7000, days: 4);

        var r = await h.Applications.CompareAsync(employer.Id, false, job.Id, "match");

        Assert.True(r.Succeeded);
        var rows = r.Value!.Rows;
        Assert.Equal("strong", rows[0].Name);            // sorted by skill match
        Assert.Equal(100, rows[0].Match.Percent);
        Assert.Equal(0, rows[1].Match.Percent);          // has C# but below Intermediate, no SQL
        Assert.True(rows.Single(x => x.Name == "weak").IsLowestPrice);
        Assert.True(rows.Single(x => x.Name == "weak").IsFastest);
        Assert.All(rows, x => Assert.Null(x.AverageRating)); // no fabricated ratings
    }

    [Fact]
    public async Task Other_employer_cannot_compare_applicants()
    {
        using var h = new TestHarness();
        var job = h.AddJob(h.AddUser("emp"));
        var r = await h.Applications.CompareAsync(h.AddUser("intruder").Id, false, job.Id, "applied");
        Assert.False(r.Succeeded);
    }
}

public class AssignmentWorkflowTests
{
    private static async Task<(TestHarness h, Models.ApplicationUser emp, Models.ApplicationUser hunter, Models.ApplicationUser loser, int assignmentId, int jobId)> SelectedAsync()
    {
        var h = new TestHarness();
        var emp = h.AddUser("emp");
        var hunter = h.AddUser("hunter");
        var loser = h.AddUser("loser");
        var job = h.AddJob(emp);
        var app = (await h.ApplyAsync(hunter, job)).Value;
        await h.ApplyAsync(loser, job);
        var assignmentId = (await h.Applications.SelectAsync(emp.Id, app)).Value;
        return (h, emp, hunter, loser, assignmentId, job.Id);
    }

    [Fact]
    public async Task Full_lifecycle_submit_revise_resubmit_approve_pay_rate()
    {
        var (h, emp, hunter, loser, aid, jobId) = await SelectedAsync();
        using (h)
        {
            // Accept: other applicant rejected, payment ledger created
            Assert.True((await h.Assignments.AcceptAsync(hunter.Id, aid)).Succeeded);
            Assert.Equal(ApplicationStatus.Rejected, (await h.Db.JobApplications.SingleAsync(a => a.HunterId == loser.Id)).Status);
            var payment = await h.Db.Payments.SingleAsync(p => p.TaskAssignmentId == aid);
            Assert.Equal(PaymentStatus.Pending, payment.Status);

            // Submit v1 -> awaiting approval
            var s1 = (await h.SubmitLinkAsync(hunter, aid)).Value;
            Assert.Equal(PaymentStatus.AwaitingApproval, payment.Status);
            Assert.Equal(JobStatus.UnderReview, (await h.Db.Jobs.FindAsync(jobId))!.Status);

            // Hunter cannot approve own work
            Assert.False((await h.Assignments.ApproveAsync(hunter.Id, s1, null)).Succeeded);

            // Revision requested (reason required)
            Assert.False((await h.Assignments.RequestRevisionAsync(emp.Id, s1, "")).Succeeded);
            Assert.True((await h.Assignments.RequestRevisionAsync(emp.Id, s1, "Please add input validation.")).Succeeded);
            Assert.Equal(PaymentStatus.Pending, payment.Status);

            // Resubmit v2 -> approve
            h.Clock.Advance(TimeSpan.FromHours(2));
            var s2 = (await h.SubmitLinkAsync(hunter, aid, "Added validation as requested.")).Value;
            Assert.Equal(2, (await h.Db.WorkSubmissions.FindAsync(s2))!.Version);
            Assert.Equal(2, await h.Db.WorkSubmissions.CountAsync(s => s.TaskAssignmentId == aid)); // history preserved

            // Cannot pay before approval
            Assert.False((await h.Payments.MarkPaidAsync(emp.Id, payment.Id, PaymentMethod.Bkash, null, null)).Succeeded);

            Assert.True((await h.Assignments.ApproveAsync(emp.Id, s2, "Great work")).Succeeded);
            var assignment = await h.Db.TaskAssignments.FindAsync(aid);
            Assert.Equal(AssignmentStatus.Completed, assignment!.Status);
            Assert.Equal(JobStatus.Completed, (await h.Db.Jobs.FindAsync(jobId))!.Status);
            Assert.Equal(PaymentStatus.Approved, payment.Status);

            // Only the owning employer can record payment
            Assert.False((await h.Payments.MarkPaidAsync(hunter.Id, payment.Id, PaymentMethod.Bkash, null, null)).Succeeded);
            Assert.True((await h.Payments.MarkPaidAsync(emp.Id, payment.Id, PaymentMethod.Bkash, "TRX123", null)).Succeeded);
            Assert.Equal(PaymentStatus.Paid, payment.Status);
            Assert.StartsWith("SIM-", payment.LedgerReference);
            Assert.True(await h.Db.PaymentEvents.CountAsync(e => e.PaymentId == payment.Id) >= 5);

            // Ratings: each side once; outsiders cannot rate
            Assert.True((await h.Ratings.CreateAsync(emp.Id, aid, 5, "Excellent")).Succeeded);
            Assert.False((await h.Ratings.CreateAsync(emp.Id, aid, 4, "again")).Succeeded);
            Assert.False((await h.Ratings.CreateAsync(loser.Id, aid, 1, "spite")).Succeeded);
            Assert.True((await h.Ratings.CreateAsync(hunter.Id, aid, 4, null)).Succeeded);
            var rep = await h.Reputation.ForUserAsync(hunter.Id);
            Assert.Equal(5.0, rep.AverageRating);
            Assert.Equal(1, rep.CompletedJobs);
        }
    }

    [Fact]
    public async Task Rating_is_not_allowed_before_completion()
    {
        var (h, emp, hunter, _, aid, _) = await SelectedAsync();
        using (h)
        {
            await h.Assignments.AcceptAsync(hunter.Id, aid);
            Assert.False((await h.Ratings.CreateAsync(emp.Id, aid, 5, null)).Succeeded);
        }
    }

    [Fact]
    public async Task Rating_must_be_between_one_and_five()
    {
        var (h, emp, _, _, aid, _) = await SelectedAsync();
        using (h)
        {
            Assert.False((await h.Ratings.CreateAsync(emp.Id, aid, 6, null)).Succeeded);
            Assert.False((await h.Ratings.CreateAsync(emp.Id, aid, 0, null)).Succeeded);
        }
    }

    [Fact]
    public async Task Declining_reopens_the_job_for_another_selection()
    {
        var (h, emp, hunter, loser, aid, jobId) = await SelectedAsync();
        using (h)
        {
            Assert.True((await h.Assignments.DeclineAsync(hunter.Id, aid, "Busy")).Succeeded);
            Assert.Equal(JobStatus.Open, (await h.Db.Jobs.FindAsync(jobId))!.Status);
            var loserApp = await h.Db.JobApplications.SingleAsync(a => a.HunterId == loser.Id);
            Assert.True((await h.Applications.SelectAsync(emp.Id, loserApp.Id)).Succeeded);
        }
    }

    [Fact]
    public async Task Another_hunter_cannot_accept_or_submit()
    {
        var (h, _, _, loser, aid, _) = await SelectedAsync();
        using (h)
        {
            Assert.False((await h.Assignments.AcceptAsync(loser.Id, aid)).Succeeded);
            Assert.False((await h.SubmitLinkAsync(loser, aid)).Succeeded);
        }
    }

    [Fact]
    public async Task Submission_requires_a_file_or_link()
    {
        var (h, _, hunter, _, aid, _) = await SelectedAsync();
        using (h)
        {
            await h.Assignments.AcceptAsync(hunter.Id, aid);
            var r = await h.Assignments.SubmitAsync(hunter.Id, new ViewModels.SubmitWorkModel { AssignmentId = aid, Description = "No attachment at all" });
            Assert.False(r.Succeeded);
        }
    }
}

public class ChallengeWorkflowTests
{
    [Fact]
    public async Task First_valid_submission_wins_and_only_winner_is_paid()
    {
        using var h = new TestHarness();
        var emp = h.AddUser("emp");
        var p1 = h.AddUser("p1");
        var p2 = h.AddUser("p2");
        var p3 = h.AddUser("p3");
        var p4 = h.AddUser("p4");
        var job = h.AddJob(emp, JobMode.Challenge, maxParticipants: 3, paymentType: PaymentType.FixedPrice, budget: 3000);

        Assert.False((await h.Assignments.JoinChallengeAsync(p1.Id, job.Id, acceptedTerms: false)).Succeeded); // rules must be accepted
        var a1 = (await h.Assignments.JoinChallengeAsync(p1.Id, job.Id, true)).Value;
        var a2 = (await h.Assignments.JoinChallengeAsync(p2.Id, job.Id, true)).Value;
        var a3 = (await h.Assignments.JoinChallengeAsync(p3.Id, job.Id, true)).Value;
        Assert.False((await h.Assignments.JoinChallengeAsync(p4.Id, job.Id, true)).Succeeded);  // full
        Assert.False((await h.ApplyAsync(p4, job)).Succeeded);                                   // no normal applications

        // p2 submits first, then p1, then p3 (server timestamps)
        var s2 = (await h.SubmitLinkAsync(p2, a2)).Value;
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var s1 = (await h.SubmitLinkAsync(p1, a1)).Value;
        h.Clock.Advance(TimeSpan.FromMinutes(5));
        var s3 = (await h.SubmitLinkAsync(p3, a3)).Value;

        // Must review in order: approving p1 before p2 is refused
        Assert.False((await h.Assignments.ApproveAsync(emp.Id, s1, null)).Succeeded);

        // p2 fails review -> p1 becomes reviewable and wins
        Assert.True((await h.Assignments.RejectChallengeSubmissionAsync(emp.Id, s2, "Does not meet the size requirement")).Succeeded);
        Assert.True((await h.Assignments.ApproveAsync(emp.Id, s1, "Winner")).Succeeded);

        var dbJob = await h.Db.Jobs.FindAsync(job.Id);
        Assert.Equal(a1, dbJob!.WinnerAssignmentId);
        Assert.Equal(JobStatus.Completed, dbJob.Status);
        Assert.True((await h.Db.TaskAssignments.FindAsync(a1))!.IsWinner);
        Assert.Equal(AssignmentStatus.ChallengeLost, (await h.Db.TaskAssignments.FindAsync(a2))!.Status);
        Assert.Equal(AssignmentStatus.ChallengeLost, (await h.Db.TaskAssignments.FindAsync(a3))!.Status);
        Assert.Equal(SubmissionStatus.Closed, (await h.Db.WorkSubmissions.FindAsync(s3))!.Status);

        // Exactly one payment, for the winner, approved (not paid)
        var payments = await h.Db.Payments.ToListAsync();
        var single = Assert.Single(payments);
        Assert.Equal(p1.Id, single.HunterId);
        Assert.Equal(PaymentStatus.Approved, single.Status);
        Assert.Equal(3000, single.Amount);

        // No second winner, no late resubmission
        Assert.False((await h.Assignments.ApproveAsync(emp.Id, s3, null)).Succeeded);
        Assert.False((await h.SubmitLinkAsync(p2, a2)).Succeeded);
        Assert.Equal(1, await h.Db.TaskAssignments.CountAsync(a => a.JobId == job.Id && a.IsWinner));
    }

    [Fact]
    public async Task Challenge_submission_after_deadline_is_refused()
    {
        using var h = new TestHarness();
        var emp = h.AddUser("emp");
        var p1 = h.AddUser("p1");
        var job = h.AddJob(emp, JobMode.Challenge, maxParticipants: 2, paymentType: PaymentType.FixedPrice);
        var a1 = (await h.Assignments.JoinChallengeAsync(p1.Id, job.Id, true)).Value;
        h.Clock.Advance(TimeSpan.FromDays(21));
        Assert.False((await h.SubmitLinkAsync(p1, a1)).Succeeded);
    }

    [Fact]
    public async Task Employer_cannot_join_own_challenge()
    {
        using var h = new TestHarness();
        var emp = h.AddUser("emp");
        var job = h.AddJob(emp, JobMode.Challenge, maxParticipants: 2, paymentType: PaymentType.FixedPrice);
        Assert.False((await h.Assignments.JoinChallengeAsync(emp.Id, job.Id, true)).Succeeded);
    }
}

public class NotificationAndJobTests
{
    [Fact]
    public async Task Users_cannot_read_other_users_notifications()
    {
        using var h = new TestHarness();
        var a = h.AddUser("a");
        var b = h.AddUser("b");
        h.Notifications.Add(a.Id, NotificationType.General, "Hi", "Only for A");
        await h.Db.SaveChangesAsync();
        var id = (await h.Db.Notifications.SingleAsync()).Id;

        Assert.Null(await h.Notifications.MarkReadAsync(b.Id, id));
        Assert.False((await h.Db.Notifications.FindAsync(id))!.IsRead);
        Assert.Equal(0, (await h.Notifications.ListAsync(b.Id, 1)).TotalCount);
        Assert.Equal(1, await h.Notifications.UnreadCountAsync(a.Id));
    }

    [Fact]
    public async Task Search_filters_against_real_data()
    {
        using var h = new TestHarness();
        var emp = h.AddUser("emp");
        var cheap = h.AddJob(emp, budget: 1000);
        var pricey = h.AddJob(emp, budget: 50000);
        pricey.Difficulty = JobDifficulty.ThreeStar;
        await h.Db.SaveChangesAsync();

        var byBudget = await h.Jobs.SearchAsync(new ViewModels.JobSearchQuery { MinBudget = 20000 });
        Assert.Equal(new[] { pricey.Id }, byBudget.Items.Select(i => i.Id));

        var byDifficulty = await h.Jobs.SearchAsync(new ViewModels.JobSearchQuery { Difficulty = JobDifficulty.TwoStar });
        Assert.Equal(new[] { cheap.Id }, byDifficulty.Items.Select(i => i.Id));

        var bySkill = await h.Jobs.SearchAsync(new ViewModels.JobSearchQuery { SkillId = 3 });
        Assert.Equal(2, bySkill.TotalCount);

        h.Clock.Advance(TimeSpan.FromDays(6)); // past application deadline -> hidden by default
        Assert.Equal(0, (await h.Jobs.SearchAsync(new ViewModels.JobSearchQuery())).TotalCount);
        Assert.Equal(2, (await h.Jobs.SearchAsync(new ViewModels.JobSearchQuery { AllStatuses = true })).TotalCount);
    }

    [Fact]
    public async Task Employer_cannot_edit_or_cancel_another_employers_job()
    {
        using var h = new TestHarness();
        var owner = h.AddUser("owner");
        var other = h.AddUser("other");
        var job = h.AddJob(owner);
        Assert.False((await h.Jobs.CancelAsync(other.Id, false, job.Id, null)).Succeeded);
        Assert.False((await h.Jobs.CloseAsync(other.Id, false, job.Id)).Succeeded);
        Assert.True((await h.Jobs.CancelAsync(owner.Id, false, job.Id, null)).Succeeded);
    }
}
