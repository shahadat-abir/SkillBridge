using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Authorization;
using SkillBridge.Data;
using SkillBridge.Domain;
using SkillBridge.Models.Enums;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

public class JobsController : AppControllerBase
{
    private readonly JobService _jobs;
    private readonly ApplicationDbContext _db;
    private readonly FileStorageService _files;
    private readonly TimeProvider _clock;

    public JobsController(JobService jobs, ApplicationDbContext db, FileStorageService files, TimeProvider clock)
    {
        _jobs = jobs;
        _db = db;
        _files = files;
        _clock = clock;
    }

    // ------------------------------------------------ public browsing

    public async Task<IActionResult> Index([FromQuery] JobSearchQuery query)
    {
        var vm = new JobSearchViewModel
        {
            Query = query,
            Results = await _jobs.SearchAsync(query),
            Categories = new SelectList(await _db.JobCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(), "Id", "Name", query.CategoryId),
            Skills = new SelectList(await _db.Skills.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name", query.SkillId)
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var job = await _jobs.GetWithDetailsAsync(id);
        if (job is null) return NotFound();
        var now = _clock.GetUtcNow().UtcDateTime;
        var vm = new JobDetailsViewModel
        {
            Job = job,
            IsOwner = job.EmployerId == UserId,
            IsAdmin = IsAdmin,
            IsHunter = IsHunter,
            IsAccepting = JobRules.IsAcceptingApplications(job, now),
            ActiveParticipants = job.Assignments.Count(a => a.IsChallengeParticipant && a.Status != AssignmentStatus.Cancelled),
            ApplicationsCount = await _db.JobApplications.CountAsync(a => a.JobId == id && a.Status != ApplicationStatus.Withdrawn),
            CurrentAssignment = job.Mode == JobMode.Standard
                ? job.Assignments.FirstOrDefault(a => a.Status is not (AssignmentStatus.Declined or AssignmentStatus.Cancelled))
                : null
        };

        if (IsHunter)
        {
            vm.MyApplication = await _db.JobApplications.AsNoTracking().FirstOrDefaultAsync(a => a.JobId == id && a.HunterId == UserId);
            vm.MyAssignment = job.Assignments.FirstOrDefault(a => a.HunterId == UserId);
            var mySkills = await _db.UserSkills.AsNoTracking().Where(s => s.UserId == UserId)
                .Select(s => new HunterSkillLevel(s.SkillId, s.Level, s.YearsOfExperience)).ToListAsync();
            vm.MyMatch = SkillMatcher.Match(job.RequiredSkills.Select(s => new RequiredSkill(s.SkillId, s.Skill.Name, s.MinimumLevel)), mySkills);
            if (job.Mode == JobMode.Challenge)
            {
                if (vm.MyAssignment != null)
                {
                    vm.CanJoinChallenge = false;
                    vm.JoinBlockedReason = "You are registered for this challenge.";
                }
                else
                {
                    vm.CanJoinChallenge = ChallengeRules.CanJoin(job, vm.ActiveParticipants, now, out var reason);
                    vm.JoinBlockedReason = reason;
                }
            }
        }
        return View(vm);
    }

    [Authorize]
    public async Task<IActionResult> Attachment(int id)
    {
        var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
        if (job?.AttachmentPath is null) return NotFound();
        var file = _files.Open(job.AttachmentPath);
        if (file is null) return NotFound();
        return File(file.Value.Stream, file.Value.ContentType, job.AttachmentOriginalName ?? "attachment");
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Report(int id, string reason)
    {
        var r = await _jobs.ReportAsync(UserId, id, reason);
        return Flash(r, "Thank you. The report was sent to the administrator.", RedirectToAction(nameof(Details), new { id }));
    }

    // ------------------------------------------------ employer management

    [Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> Mine() => View(await _jobs.ListForEmployerAsync(UserId));

    [Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync(null);
        return View("Form", new JobFormModel { RequiredSkills = await _jobs.BuildSkillInputsAsync() });
    }

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> Create(JobFormModel model)
    {
        foreach (var (field, msg) in _jobs.ValidateForm(model, isNew: true)) ModelState.AddModelError(field, msg);
        if (ModelState.IsValid)
        {
            var r = await _jobs.CreateAsync(UserId, model);
            if (r.Succeeded)
            {
                Success("Your job was published.");
                return RedirectToAction(nameof(Details), new { id = r.Value });
            }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        model.RequiredSkills = await _jobs.BuildSkillInputsAsync(posted: model.RequiredSkills);
        await LoadCategoriesAsync(model.CategoryId);
        return View("Form", model);
    }

    [Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> Edit(int id)
    {
        var job = await _db.Jobs.Include(j => j.RequiredSkills).FirstOrDefaultAsync(j => j.Id == id);
        if (job is null) return NotFound();
        if (job.EmployerId != UserId) return Forbid();
        if (!JobRules.CanEdit(job))
        {
            Error("This job can no longer be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }
        var form = JobService.ToForm(job);
        form.RequiredSkills = await _jobs.BuildSkillInputsAsync(job.RequiredSkills);
        await LoadCategoriesAsync(job.CategoryId);
        return View("Form", form);
    }

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> Edit(int id, JobFormModel model)
    {
        model.Id = id;
        foreach (var (field, msg) in _jobs.ValidateForm(model, isNew: false)) ModelState.AddModelError(field, msg);
        if (ModelState.IsValid)
        {
            var r = await _jobs.UpdateAsync(UserId, id, model);
            if (r.Succeeded)
            {
                Success("Job updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        model.RequiredSkills = await _jobs.BuildSkillInputsAsync(posted: model.RequiredSkills);
        await LoadCategoriesAsync(model.CategoryId);
        return View("Form", model);
    }

    [HttpPost, Authorize(Roles = AppRoles.Employer + "," + AppRoles.Admin)]
    public async Task<IActionResult> Close(int id) =>
        Flash(await _jobs.CloseAsync(UserId, IsAdmin, id), "The job no longer accepts applications.", RedirectToAction(nameof(Details), new { id }));

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> Reopen(int id) =>
        Flash(await _jobs.ReopenAsync(UserId, id), "The job is open for applications again.", RedirectToAction(nameof(Details), new { id }));

    [HttpPost, Authorize(Roles = AppRoles.Employer + "," + AppRoles.Admin)]
    public async Task<IActionResult> Cancel(int id, string? reason) =>
        Flash(await _jobs.CancelAsync(UserId, IsAdmin, id, reason), "The job was cancelled.", RedirectToAction(nameof(Details), new { id }));

    private async Task LoadCategoriesAsync(int? selected) =>
        ViewBag.Categories = new SelectList(await _db.JobCategories.Where(c => c.IsActive || c.Id == selected).OrderBy(c => c.Name).ToListAsync(), "Id", "Name", selected);
}
