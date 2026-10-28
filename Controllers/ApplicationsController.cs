using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Authorization;
using SkillBridge.Data;
using SkillBridge.Domain;
using SkillBridge.Models.Enums;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

[Authorize]
public class ApplicationsController : AppControllerBase
{
    private readonly ApplicationService _apps;
    private readonly ApplicationDbContext _db;
    private readonly FileStorageService _files;
    private readonly TimeProvider _clock;

    public ApplicationsController(ApplicationService apps, ApplicationDbContext db, FileStorageService files, TimeProvider clock)
    {
        _apps = apps;
        _db = db;
        _files = files;
        _clock = clock;
    }

    // ------------------------------------------------ Hunter

    [Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Apply(int id)
    {
        var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
        if (job is null) return NotFound();
        if (job.Mode != JobMode.Standard || !JobRules.IsAcceptingApplications(job, _clock.GetUtcNow().UtcDateTime))
        {
            Error("This job is not accepting applications.");
            return RedirectToAction("Details", "Jobs", new { id });
        }
        if (await _db.JobApplications.AnyAsync(a => a.JobId == id && a.HunterId == UserId))
        {
            Error("You have already applied to this job.");
            return RedirectToAction(nameof(Mine));
        }
        return View(new ApplyFormModel
        {
            JobId = job.Id,
            JobTitle = job.Title,
            JobBudget = job.Budget,
            JobExpectedDays = job.ExpectedCompletionDays,
            PaymentType = job.PaymentType,
            ProposedAmount = job.Budget,
            ProposedDays = job.ExpectedCompletionDays
        });
    }

    [HttpPost, Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Apply(int id, ApplyFormModel model)
    {
        model.JobId = id;
        if (ModelState.IsValid)
        {
            var r = await _apps.ApplyAsync(UserId, model);
            if (r.Succeeded)
            {
                Success("Your application was submitted. You can follow its status under My applications.");
                return RedirectToAction(nameof(Mine));
            }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
        if (job is null) return NotFound();
        model.JobTitle = job.Title;
        model.JobBudget = job.Budget;
        model.JobExpectedDays = job.ExpectedCompletionDays;
        model.PaymentType = job.PaymentType;
        return View(model);
    }

    [Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Mine() => View(await _apps.ListForHunterAsync(UserId));

    [HttpPost, Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Withdraw(int id) =>
        Flash(await _apps.WithdrawAsync(UserId, id), "Application withdrawn.", RedirectToAction(nameof(Mine)));

    // ------------------------------------------------ Employer

    [Authorize(Roles = AppRoles.Employer + "," + AppRoles.Admin)]
    public async Task<IActionResult> ForJob(int id)
    {
        var r = await _apps.ForJobAsync(UserId, IsAdmin, id);
        if (!r.Succeeded) return r.Error == "Job not found." ? NotFound() : Forbid();
        return View(r.Value);
    }

    /// <summary>Side-by-side comparison. Optional ?ids=1&ids=2 limits the comparison to chosen applicants.</summary>
    [Authorize(Roles = AppRoles.Employer + "," + AppRoles.Admin)]
    public async Task<IActionResult> Compare(int id, string sort = "applied", [FromQuery] List<int>? ids = null)
    {
        var r = await _apps.CompareAsync(UserId, IsAdmin, id, sort, ids);
        if (!r.Succeeded) return r.Error == "Job not found." ? NotFound() : Forbid();
        return View(r.Value);
    }

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> ChangeStatus(int id, ApplicationStatus status, string? note, int jobId, string? returnTo)
    {
        var r = await _apps.ChangeStatusAsync(UserId, id, status, note);
        var redirect = returnTo == "compare" ? RedirectToAction(nameof(Compare), new { id = jobId }) : RedirectToAction(nameof(ForJob), new { id = jobId });
        return Flash(r, $"Application marked as {status}.", redirect);
    }

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> Select(int id, int jobId)
    {
        var r = await _apps.SelectAsync(UserId, id);
        if (r.Succeeded)
        {
            Success("Hunter selected. They have been notified and must accept the assignment.");
            return RedirectToAction("Details", "Assignments", new { id = r.Value });
        }
        Error(r.Error!);
        return RedirectToAction(nameof(Compare), new { id = jobId });
    }

    public async Task<IActionResult> Attachment(int id)
    {
        var app = await _apps.GetForDownloadAsync(UserId, IsAdmin, id);
        if (app?.AttachmentPath is null) return NotFound();
        var file = _files.Open(app.AttachmentPath);
        if (file is null) return NotFound();
        return File(file.Value.Stream, file.Value.ContentType, app.AttachmentOriginalName ?? "attachment");
    }
}
