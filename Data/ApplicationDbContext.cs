using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Models;

namespace SkillBridge.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<JobCategory> JobCategories => Set<JobCategory>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<WorkSubmission> WorkSubmissions => Set<WorkSubmission>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity tables

        // Historical business records are never cascade-deleted (Restrict); only owned detail rows cascade.

        builder.Entity<JobCategory>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
            e.HasData(
                new JobCategory { Id = 1, Name = "Web Development", Description = "Websites, web apps and APIs" },
                new JobCategory { Id = 2, Name = "Mobile Development", Description = "Android, iOS and cross-platform apps" },
                new JobCategory { Id = 3, Name = "Data Science & Analytics", Description = "Data analysis, ML and reporting" },
                new JobCategory { Id = 4, Name = "Design & Creative", Description = "UI/UX, graphics, video" },
                new JobCategory { Id = 5, Name = "Writing & Translation", Description = "Content, documentation, translation" },
                new JobCategory { Id = 6, Name = "Marketing", Description = "Digital marketing and SEO" },
                new JobCategory { Id = 7, Name = "Other", Description = "Anything else" });
        });

        builder.Entity<Skill>(e =>
        {
            e.HasIndex(s => s.Name).IsUnique();
            e.HasData(
                new Skill { Id = 1,  Name = "C#",                 Category = "Programming" },
                new Skill { Id = 2,  Name = "ASP.NET Core",       Category = "Web Development" },
                new Skill { Id = 3,  Name = "SQL",                Category = "Database" },
                new Skill { Id = 4,  Name = "JavaScript",         Category = "Programming" },
                new Skill { Id = 5,  Name = "React",              Category = "Web Development" },
                new Skill { Id = 6,  Name = "Python",             Category = "Programming" },
                new Skill { Id = 7,  Name = "Data Analysis",      Category = "Data Science" },
                new Skill { Id = 8,  Name = "Machine Learning",   Category = "Data Science" },
                new Skill { Id = 9,  Name = "UI/UX Design",       Category = "Design" },
                new Skill { Id = 10, Name = "Graphic Design",     Category = "Design" },
                new Skill { Id = 11, Name = "Content Writing",    Category = "Writing" },
                new Skill { Id = 12, Name = "Digital Marketing",  Category = "Marketing" },
                new Skill { Id = 13, Name = "Flutter",            Category = "Mobile Development" },
                new Skill { Id = 14, Name = "Video Editing",      Category = "Multimedia" },
                new Skill { Id = 15, Name = "Bangla-English Translation", Category = "Writing" });
        });

        builder.Entity<UserSkill>(e =>
        {
            e.HasKey(us => new { us.UserId, us.SkillId });
            e.HasOne(us => us.User).WithMany(u => u.Skills).HasForeignKey(us => us.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(us => us.Skill).WithMany(s => s.UserSkills).HasForeignKey(us => us.SkillId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Job>(e =>
        {
            e.HasOne(j => j.Employer).WithMany(u => u.PostedJobs).HasForeignKey(j => j.EmployerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(j => j.Category).WithMany(c => c.Jobs).HasForeignKey(j => j.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(j => j.Status);
            e.HasIndex(j => j.CategoryId);
            e.HasIndex(j => j.Difficulty);
            e.HasIndex(j => j.ApplicationDeadline);
            e.HasIndex(j => j.EmployerId);
        });

        builder.Entity<JobSkill>(e =>
        {
            e.HasKey(js => new { js.JobId, js.SkillId });
            e.HasOne(js => js.Job).WithMany(j => j.RequiredSkills).HasForeignKey(js => js.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(js => js.Skill).WithMany(s => s.JobSkills).HasForeignKey(js => js.SkillId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobApplication>(e =>
        {
            e.HasOne(a => a.Job).WithMany(j => j.Applications).HasForeignKey(a => a.JobId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Hunter).WithMany(u => u.Applications).HasForeignKey(a => a.HunterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(a => new { a.JobId, a.HunterId }).IsUnique(); // one application per Hunter per job
            e.HasIndex(a => a.Status);
        });

        builder.Entity<TaskAssignment>(e =>
        {
            e.HasOne(t => t.Job).WithMany(j => j.Assignments).HasForeignKey(t => t.JobId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Hunter).WithMany(u => u.Assignments).HasForeignKey(t => t.HunterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Application).WithMany().HasForeignKey(t => t.ApplicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => new { t.JobId, t.HunterId }).IsUnique();
            e.HasIndex(t => t.ApplicationId).IsUnique().HasFilter("[ApplicationId] IS NOT NULL");
            // Database-level guarantee: at most one challenge winner per job.
            e.HasIndex(t => t.JobId, "IX_TaskAssignments_OneWinnerPerJob").IsUnique().HasFilter("[IsWinner] = 1");
        });

        builder.Entity<WorkSubmission>(e =>
        {
            e.HasOne(s => s.TaskAssignment).WithMany(t => t.Submissions).HasForeignKey(s => s.TaskAssignmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.TaskAssignmentId, s.Version }).IsUnique();
        });

        builder.Entity<Rating>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("CK_Rating_Stars", "[Stars] BETWEEN 1 AND 5"));
            e.HasOne(r => r.TaskAssignment).WithMany(t => t.Ratings).HasForeignKey(r => r.TaskAssignmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Rater).WithMany(u => u.RatingsGiven).HasForeignKey(r => r.RaterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Ratee).WithMany(u => u.RatingsReceived).HasForeignKey(r => r.RateeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.TaskAssignmentId, r.RaterId }).IsUnique(); // one review per side per assignment
        });

        builder.Entity<Payment>(e =>
        {
            e.HasOne(p => p.TaskAssignment).WithOne(t => t.Payment).HasForeignKey<Payment>(p => p.TaskAssignmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Employer).WithMany().HasForeignKey(p => p.EmployerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Hunter).WithMany().HasForeignKey(p => p.HunterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.LedgerReference).IsUnique();
        });

        builder.Entity<PaymentEvent>(e =>
        {
            e.HasOne(pe => pe.Payment).WithMany(p => p.History).HasForeignKey(pe => pe.PaymentId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User).WithMany(u => u.Notifications).HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedAt);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
        });

        builder.Entity<ContentReport>(e =>
        {
            e.HasOne(r => r.Job).WithMany().HasForeignKey(r => r.JobId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => r.Status);
        });
    }
}
