using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Authorization;
using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.Data;

/// <summary>
/// Creates the database, roles, the administrator and (optionally, Development only) clearly-labelled demo data.
/// No passwords are hard-coded: they come from configuration (user secrets / environment variables) or are
/// generated randomly and written to App_Data/dev-credentials.txt (git-ignored) in Development.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SkillBridge.Seeder");
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        if (db.Database.IsRelational() && db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();          // preferred: EF Core migrations
        else
            await db.Database.EnsureCreatedAsync();    // fallback when no migration has been generated yet

        foreach (var role in AppRoles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var credentialLines = new List<string>();

        // ---- Administrator
        var adminEmail = config["Seed:AdminEmail"] ?? "admin@skillbridge.local";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var pwd = config["Seed:AdminPassword"];
            var generated = string.IsNullOrWhiteSpace(pwd);
            if (generated && env.IsDevelopment()) pwd = GeneratePassword();

            if (string.IsNullOrWhiteSpace(pwd))
            {
                logger.LogWarning("No Seed:AdminPassword configured; administrator account not created.");
            }
            else
            {
                var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, FullName = "SkillBridge Administrator", EmailConfirmed = true };
                var res = await userManager.CreateAsync(admin, pwd);
                if (res.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, AppRoles.Admin);
                    if (generated) credentialLines.Add($"Admin: {adminEmail} / {pwd}");
                }
                else logger.LogError("Admin seed failed: {Errors}", string.Join("; ", res.Errors.Select(e => e.Description)));
            }
        }

        // ---- Demo data (Development + Seed:DemoData=true only). Clearly labelled, no fake ratings/payments.
        if (env.IsDevelopment() && config.GetValue<bool>("Seed:DemoData") && !await db.Users.AnyAsync(u => u.IsDemoAccount))
        {
            var demoPwd = config["Seed:DemoPassword"];
            if (string.IsNullOrWhiteSpace(demoPwd)) demoPwd = GeneratePassword();

            var employer = await CreateDemoUser(userManager, "employer.demo@skillbridge.local", "[DEMO] Karim Traders", AppRoles.Employer, demoPwd, u =>
            {
                u.CompanyName = "[DEMO] Karim Traders Ltd.";
                u.Headline = "Demo employer account";
                u.Location = "Chattogram";
            });
            var h1 = await CreateDemoUser(userManager, "hunter1.demo@skillbridge.local", "[DEMO] Nusrat Jahan", AppRoles.Hunter, demoPwd, u =>
            {
                u.Headline = "ASP.NET Core developer (demo profile)";
                u.Education = "BSc in CSE (demo)";
                u.Experience = "Built two MVC web apps for university projects (demo).";
                u.Availability = "20 hours/week";
                u.Location = "Chattogram";
            });
            var h2 = await CreateDemoUser(userManager, "hunter2.demo@skillbridge.local", "[DEMO] Rafiq Hasan", AppRoles.Hunter, demoPwd, u =>
            {
                u.Headline = "Data analyst (demo profile)";
                u.Education = "BSc in Statistics (demo)";
                u.Availability = "Full time";
            });
            var h3 = await CreateDemoUser(userManager, "hunter3.demo@skillbridge.local", "[DEMO] Mithila Roy", AppRoles.Hunter, demoPwd, u =>
            {
                u.Headline = "UI/UX designer (demo profile)";
                u.Availability = "Weekends";
            });

            if (employer != null && h1 != null && h2 != null && h3 != null)
            {
                db.UserSkills.AddRange(
                    new UserSkill { UserId = h1.Id, SkillId = 1, Level = SkillLevel.Advanced, YearsOfExperience = 2 },
                    new UserSkill { UserId = h1.Id, SkillId = 2, Level = SkillLevel.Intermediate, YearsOfExperience = 1 },
                    new UserSkill { UserId = h1.Id, SkillId = 3, Level = SkillLevel.Intermediate, YearsOfExperience = 2 },
                    new UserSkill { UserId = h2.Id, SkillId = 6, Level = SkillLevel.Advanced, YearsOfExperience = 3 },
                    new UserSkill { UserId = h2.Id, SkillId = 7, Level = SkillLevel.Expert, YearsOfExperience = 4 },
                    new UserSkill { UserId = h2.Id, SkillId = 3, Level = SkillLevel.Beginner, YearsOfExperience = 1 },
                    new UserSkill { UserId = h3.Id, SkillId = 9, Level = SkillLevel.Advanced, YearsOfExperience = 3 },
                    new UserSkill { UserId = h3.Id, SkillId = 10, Level = SkillLevel.Intermediate, YearsOfExperience = 2 });

                var now = DateTime.UtcNow;
                var job1 = new Job
                {
                    Title = "[DEMO] Build an inventory dashboard in ASP.NET Core",
                    Description = "Demo job. Create a small ASP.NET Core MVC dashboard that lists products, stock levels and low-stock alerts from a SQL Server database.",
                    CategoryId = 1, Difficulty = JobDifficulty.TwoStar, Mode = JobMode.Standard,
                    Budget = 15000, PaymentType = PaymentType.Negotiable, ExpectedCompletionDays = 10,
                    ApplicationDeadline = now.AddDays(10), ProjectDeadline = now.AddDays(30),
                    EligibilityCriteria = "Experience with C# and SQL.", SubmissionInstructions = "Share a GitHub link and a short demo video.",
                    EmployerId = employer.Id, IsDemoData = true, CreatedAt = now, UpdatedAt = now
                };
                job1.RequiredSkills.Add(new JobSkill { SkillId = 1, MinimumLevel = SkillLevel.Intermediate });
                job1.RequiredSkills.Add(new JobSkill { SkillId = 2, MinimumLevel = SkillLevel.Intermediate });
                job1.RequiredSkills.Add(new JobSkill { SkillId = 3, MinimumLevel = SkillLevel.Beginner });

                var job2 = new Job
                {
                    Title = "[DEMO] Challenge: design a logo for a tea brand",
                    Description = "Demo challenge. Up to 3 Hunters work on the same brief. The first submission that passes the employer's review wins the reward.",
                    CategoryId = 4, Difficulty = JobDifficulty.OneStar, Mode = JobMode.Challenge, MaxParticipants = 3,
                    Budget = 3000, PaymentType = PaymentType.FixedPrice, ExpectedCompletionDays = 3,
                    ApplicationDeadline = now.AddDays(5), ProjectDeadline = now.AddDays(8),
                    SubmissionInstructions = "Upload a PNG (min 1000x1000) and a short description of the concept.",
                    Terms = "Only the winning design is paid. Non-winning designs remain the property of their authors.",
                    EmployerId = employer.Id, IsDemoData = true, CreatedAt = now, UpdatedAt = now
                };
                job2.RequiredSkills.Add(new JobSkill { SkillId = 10, MinimumLevel = SkillLevel.Beginner });

                db.Jobs.AddRange(job1, job2);
                await db.SaveChangesAsync();
                credentialLines.Add($"Demo accounts (password for all): {demoPwd}");
                credentialLines.Add("  employer.demo@skillbridge.local, hunter1.demo@skillbridge.local, hunter2.demo@skillbridge.local, hunter3.demo@skillbridge.local");
            }
        }

        if (credentialLines.Count > 0 && env.IsDevelopment())
        {
            var dir = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "dev-credentials.txt");
            await File.AppendAllLinesAsync(file, new[] { $"--- generated {DateTime.Now:yyyy-MM-dd HH:mm} (development only, do not commit) ---" }.Concat(credentialLines));
            logger.LogWarning("Development credentials were generated and written to {File}", file);
        }
    }

    private static async Task<ApplicationUser?> CreateDemoUser(UserManager<ApplicationUser> um, string email, string name, string role, string password, Action<ApplicationUser> configure)
    {
        var user = new ApplicationUser { UserName = email, Email = email, FullName = name, EmailConfirmed = true, IsDemoAccount = true };
        configure(user);
        var res = await um.CreateAsync(user, password);
        if (!res.Succeeded) return null;
        await um.AddToRoleAsync(user, role);
        return user;
    }

    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        var core = Convert.ToBase64String(bytes).Replace("+", "x").Replace("/", "y").Replace("=", "");
        return $"Sb#{core}9a";
    }
}
