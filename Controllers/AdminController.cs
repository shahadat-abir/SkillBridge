using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Authorization;
using SkillBridge.Data;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

/// <summary>Administration. Every action is protected server-side by the Admin role.</summary>
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : AppControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly JobService _jobs;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users, AuditService audit, NotificationService notifications, JobService jobs)
    {
        _db = db;
        _users = users;
        _audit = audit;
        _notifications = notifications;
        _jobs = jobs;
    }

    public async Task<IActionResult> Index()
    {
        var employers = await _users.GetUsersInRoleAsync(AppRoles.Employer);
        var hunters = await _users.GetUsersInRoleAsync(AppRoles.Hunter);
        var vm = new AdminDashboardViewModel
        {
            TotalUsers = await _db.Users.CountAsync(),
            Employers = employers.Count,
            Hunters = hunters.Count,
            SuspendedUsers = await _db.Users.CountAsync(u => u.IsSuspended),
            TotalJobs = await _db.Jobs.CountAsync(),
            OpenJobs = await _db.Jobs.CountAsync(j => j.Status == JobStatus.Open),
            CompletedJobs = await _db.Jobs.CountAsync(j => j.Status == JobStatus.Completed),
            TotalApplications = await _db.JobApplications.CountAsync(),
            OpenReports = await _db.ContentReports.CountAsync(r => r.Status == ReportStatus.Open),
            LedgerPaid = await _db.Payments.Where(p => p.Status == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0,
            LedgerDue = await _db.Payments.Where(p => p.Status == PaymentStatus.Approved || p.Status == PaymentStatus.Failed).SumAsync(p => (decimal?)p.Amount) ?? 0,
            RecentAudit = await _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Id).Take(10).ToListAsync()
        };
        return View(vm);
    }

    // ------------------------------------------------ users

    public async Task<IActionResult> Users(string? q, int page = 1)
    {
        const int pageSize = 20;
        page = Math.Max(1, page);
        var users = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) users = users.Where(u => u.FullName.Contains(q) || u.Email!.Contains(q));
        var total = await users.CountAsync();
        var list = await users.OrderBy(u => u.FullName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ids = list.Select(u => u.Id).ToList();
        var roleMap = await (from ur in _db.UserRoles
                             join r in _db.Roles on ur.RoleId equals r.Id
                             where ids.Contains(ur.UserId)
                             select new { ur.UserId, r.Name }).ToListAsync();
        ViewBag.Query = q;
        return View(new PagedResult<AdminUserRow>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = list.Select(u => new AdminUserRow
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "",
                Role = string.Join(", ", roleMap.Where(r => r.UserId == u.Id).Select(r => r.Name)),
                IsSuspended = u.IsSuspended,
                IsDemo = u.IsDemoAccount,
                CreatedAt = u.CreatedAt
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Suspend(string id, string? reason)
    {
        if (id == UserId) { Error("You cannot suspend your own account."); return RedirectToAction(nameof(Users)); }
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        user.IsSuspended = true;
        user.SuspensionReason = reason?.Trim();
        await _users.UpdateAsync(user);
        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await _users.UpdateSecurityStampAsync(user); // signs the user out
        _notifications.Add(user.Id, NotificationType.AccountStatus, "Account suspended", $"Your account was suspended. Reason: {reason ?? "not given"}");
        _audit.Add(UserId, "Admin.User.Suspend", nameof(ApplicationUser), user.Id, reason);
        await _db.SaveChangesAsync();
        Success($"{user.FullName} was suspended.");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> Reactivate(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        user.IsSuspended = false;
        user.SuspensionReason = null;
        await _users.UpdateAsync(user);
        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);
        _notifications.Add(user.Id, NotificationType.AccountStatus, "Account reactivated", "Your account has been reactivated.");
        _audit.Add(UserId, "Admin.User.Reactivate", nameof(ApplicationUser), user.Id);
        await _db.SaveChangesAsync();
        Success($"{user.FullName} was reactivated.");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> ChangeRole(string id, string role)
    {
        if (!AppRoles.All.Contains(role)) { Error("Unknown role."); return RedirectToAction(nameof(Users)); }
        if (id == UserId) { Error("You cannot change your own role."); return RedirectToAction(nameof(Users)); }
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        var current = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, current);
        await _users.AddToRoleAsync(user, role);
        await _users.UpdateSecurityStampAsync(user);
        _audit.Add(UserId, "Admin.User.ChangeRole", nameof(ApplicationUser), user.Id, $"{string.Join(",", current)} -> {role}");
        await _db.SaveChangesAsync();
        Success($"{user.FullName} is now {role}.");
        return RedirectToAction(nameof(Users));
    }

    // ------------------------------------------------ categories & skills

    public async Task<IActionResult> Categories()
    {
        ViewBag.Skills = await _db.Skills.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return View(await _db.JobCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(string name, string? description)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 2 or > 80) { Error("Category name must be 2-80 characters."); return RedirectToAction(nameof(Categories)); }
        if (await _db.JobCategories.AnyAsync(c => c.Name == name)) { Error("That category already exists."); return RedirectToAction(nameof(Categories)); }
        _db.JobCategories.Add(new JobCategory { Name = name, Description = description?.Trim() });
        _audit.Add(UserId, "Admin.Category.Add", nameof(JobCategory), name);
        await _db.SaveChangesAsync();
        Success("Category added.");
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleCategory(int id)
    {
        var c = await _db.JobCategories.FindAsync(id);
        if (c is null) return NotFound();
        c.IsActive = !c.IsActive;
        _audit.Add(UserId, c.IsActive ? "Admin.Category.Enable" : "Admin.Category.Disable", nameof(JobCategory), id);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    public async Task<IActionResult> AddSkill(string name, string? category)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 60) { Error("Skill name must be 1-60 characters."); return RedirectToAction(nameof(Categories)); }
        if (await _db.Skills.AnyAsync(s => s.Name == name)) { Error("That skill already exists."); return RedirectToAction(nameof(Categories)); }
        _db.Skills.Add(new Skill { Name = name, Category = category?.Trim() });
        _audit.Add(UserId, "Admin.Skill.Add", nameof(Skill), name);
        await _db.SaveChangesAsync();
        Success("Skill added.");
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleSkill(int id)
    {
        var s = await _db.Skills.FindAsync(id);
        if (s is null) return NotFound();
        s.IsActive = !s.IsActive;
        _audit.Add(UserId, s.IsActive ? "Admin.Skill.Enable" : "Admin.Skill.Disable", nameof(Skill), id);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Categories));
    }

    // ------------------------------------------------ moderation

    public async Task<IActionResult> Jobs(int page = 1)
    {
        var result = await _jobs.SearchAsync(new JobSearchQuery { AllStatuses = true, Page = page });
        return View(result);
    }

    public async Task<IActionResult> Reports() =>
        View(await _db.ContentReports.AsNoTracking().Include(r => r.Job).Include(r => r.Reporter)
            .OrderBy(r => r.Status).ThenByDescending(r => r.CreatedAt).Take(200).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> ResolveReport(int id, ReportStatus status, string? note, bool cancelJob)
    {
        var report = await _db.ContentReports.Include(r => r.Job).FirstOrDefaultAsync(r => r.Id == id);
        if (report is null) return NotFound();
        if (status == ReportStatus.Open) { Error("Choose Resolved or Dismissed."); return RedirectToAction(nameof(Reports)); }
        if (cancelJob)
        {
            var r = await _jobs.CancelAsync(UserId, true, report.JobId, $"Report #{id}: {note}");
            if (!r.Succeeded) Error(r.Error!);
        }
        report.Status = status;
        report.AdminNote = note?.Trim();
        report.ResolvedAt = DateTime.UtcNow;
        _notifications.Add(report.ReporterId, NotificationType.ReportUpdate, "Report reviewed", $"Your report about \"{report.Job.Title}\" was {status.ToString().ToLowerInvariant()}.");
        _audit.Add(UserId, "Admin.Report." + status, nameof(ContentReport), id, note);
        await _db.SaveChangesAsync();
        Success("Report updated.");
        return RedirectToAction(nameof(Reports));
    }

    public async Task<IActionResult> Audit(int page = 1)
    {
        const int pageSize = 50;
        page = Math.Max(1, page);
        var total = await _db.AuditLogs.CountAsync();
        var items = await _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ids = items.Where(i => i.UserId != null).Select(i => i.UserId!).Distinct().ToList();
        ViewBag.Names = await _db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return View(new PagedResult<AuditLog> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }
}
