using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SkillBridge.Data;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.Services;

namespace SkillBridge.Tests;

/// <summary>Controllable clock for deadline tests.</summary>
public class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

/// <summary>Builds the real services over an isolated EF Core in-memory database.</summary>
public sealed class TestHarness : IDisposable
{
    public ApplicationDbContext Db { get; }
    public FakeClock Clock { get; } = new();
    public FileStorageService Files { get; }
    public NotificationService Notifications { get; }
    public AuditService Audit { get; }
    public PaymentService Payments { get; }
    public ReputationService Reputation { get; }
    public JobService Jobs { get; }
    public ApplicationService Applications { get; }
    public AssignmentService Assignments { get; }
    public RatingService Ratings { get; }
    private readonly string _uploadDir;

    public TestHarness()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("sb-" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        Db = new ApplicationDbContext(options);
        Db.Database.EnsureCreated(); // applies HasData (categories & skills)

        _uploadDir = Path.Combine(Path.GetTempPath(), "sb-tests-" + Guid.NewGuid().ToString("N"));
        Files = new FileStorageService(_uploadDir);
        Notifications = new NotificationService(Db, Clock);
        Audit = new AuditService(Db, Clock);
        Payments = new PaymentService(Db, Clock, Notifications, Audit);
        Reputation = new ReputationService(Db);
        Jobs = new JobService(Db, Clock, Files, Notifications, Audit, Payments);
        Applications = new ApplicationService(Db, Clock, Files, Notifications, Audit, Reputation);
        Assignments = new AssignmentService(Db, Clock, Files, Notifications, Audit, Payments);
        Ratings = new RatingService(Db, Clock, Notifications, Audit);
    }

    public DateTime UtcNow => Clock.GetUtcNow().UtcDateTime;

    public ApplicationUser AddUser(string name)
    {
        var u = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = name + "@test.local", Email = name + "@test.local", FullName = name };
        Db.Users.Add(u);
        Db.SaveChanges();
        return u;
    }

    public Job AddJob(ApplicationUser employer, JobMode mode = JobMode.Standard, int maxParticipants = 1,
        PaymentType paymentType = PaymentType.Negotiable, decimal budget = 10000)
    {
        var job = new Job
        {
            Title = "Test job " + Guid.NewGuid().ToString("N")[..6],
            Description = "A test job description that is long enough.",
            CategoryId = 1,
            Difficulty = JobDifficulty.TwoStar,
            Mode = mode,
            MaxParticipants = mode == JobMode.Challenge ? maxParticipants : 1,
            Budget = budget,
            PaymentType = paymentType,
            ExpectedCompletionDays = 7,
            ApplicationDeadline = UtcNow.AddDays(5),
            ProjectDeadline = UtcNow.AddDays(20),
            EmployerId = employer.Id,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };
        job.RequiredSkills.Add(new JobSkill { SkillId = 1, MinimumLevel = SkillLevel.Intermediate });
        job.RequiredSkills.Add(new JobSkill { SkillId = 3, MinimumLevel = SkillLevel.Beginner });
        Db.Jobs.Add(job);
        Db.SaveChanges();
        return job;
    }

    public Task<ServiceResult<int>> ApplyAsync(ApplicationUser hunter, Job job, decimal amount = 9000, int days = 5) =>
        Applications.ApplyAsync(hunter.Id, new ViewModels.ApplyFormModel
        {
            JobId = job.Id,
            CoverLetter = "I have done very similar work before and can deliver on time.",
            ProposedAmount = amount,
            ProposedDays = days
        });

    public Task<ServiceResult<int>> SubmitLinkAsync(ApplicationUser hunter, int assignmentId, string text = "Delivered the work as requested.") =>
        Assignments.SubmitAsync(hunter.Id, new ViewModels.SubmitWorkModel
        {
            AssignmentId = assignmentId,
            Description = text,
            ExternalLink = "https://github.com/example/repo"
        });

    public void Dispose()
    {
        Db.Dispose();
        try { if (Directory.Exists(_uploadDir)) Directory.Delete(_uploadDir, true); } catch { /* best effort */ }
    }
}
