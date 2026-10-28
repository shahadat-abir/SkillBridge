using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Authorization;
using SkillBridge.Domain;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

[Authorize]
public class AssignmentsController : AppControllerBase
{
    private readonly AssignmentService _assignments;
    private readonly FileStorageService _files;
    private readonly TimeProvider _clock;

    public AssignmentsController(AssignmentService assignments, FileStorageService files, TimeProvider clock)
    {
        _assignments = assignments;
        _files = files;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<IActionResult> Index()
    {
        if (IsEmployer) return View(new AssignmentListViewModel { Assignments = await _assignments.ListForEmployerAsync(UserId), ForEmployer = true });
        if (IsHunter) return View(new AssignmentListViewModel { Assignments = await _assignments.ListForHunterAsync(UserId) });
        return RedirectToAction("Index", "Dashboard");
    }

    public async Task<IActionResult> Details(int id)
    {
        var a = await _assignments.GetFullAsync(id);
        if (a is null) return NotFound();
        if (!AssignmentService.CanView(a, UserId, IsAdmin)) return Forbid();

        var vm = new AssignmentDetailsViewModel
        {
            Assignment = a,
            IsHunter = a.HunterId == UserId,
            IsEmployer = a.Job.EmployerId == UserId,
            IsAdmin = IsAdmin,
            CanSubmit = a.HunterId == UserId && AssignmentService.CanSubmit(a, Now),
            Ratings = a.Ratings.ToList(),
            MyRating = a.Ratings.FirstOrDefault(r => r.RaterId == UserId),
            IsLate = a.CompletedAt == null && Now > a.DueDate
        };
        vm.CanRate = (vm.IsHunter || vm.IsEmployer) && RatingService.CheckEligibility(a, UserId, a.Ratings).Succeeded;
        if (!a.IsChallengeParticipant)
            vm.NextReviewableSubmissionId = a.Submissions.Where(s => s.Status == Models.Enums.SubmissionStatus.PendingReview).Select(s => (int?)s.Id).FirstOrDefault();
        return View(vm);
    }

    [HttpPost, Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Accept(int id) =>
        Flash(await _assignments.AcceptAsync(UserId, id), "Assignment accepted. Work is now in progress.", RedirectToAction(nameof(Details), new { id }));

    [HttpPost, Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Decline(int id, string? reason) =>
        Flash(await _assignments.DeclineAsync(UserId, id, reason), "You declined the assignment.", RedirectToAction(nameof(Details), new { id }));

    [HttpPost, Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> JoinChallenge(int jobId, bool acceptRules)
    {
        var r = await _assignments.JoinChallengeAsync(UserId, jobId, acceptRules);
        if (r.Succeeded)
        {
            Success("You joined the challenge. Good luck!");
            return RedirectToAction(nameof(Details), new { id = r.Value });
        }
        Error(r.Error!);
        return RedirectToAction("Details", "Jobs", new { id = jobId });
    }

    [Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Submit(int id)
    {
        var a = await _assignments.GetFullAsync(id);
        if (a is null) return NotFound();
        if (a.HunterId != UserId) return Forbid();
        if (!AssignmentService.CanSubmit(a, Now))
        {
            Error("You cannot submit work for this assignment right now.");
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(BuildSubmitModel(a, new SubmitWorkModel()));
    }

    [HttpPost, Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Submit(int id, SubmitWorkModel model)
    {
        model.AssignmentId = id;
        if (ModelState.IsValid)
        {
            var r = await _assignments.SubmitAsync(UserId, model);
            if (r.Succeeded)
            {
                Success("Work submitted. The employer has been notified.");
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError(string.Empty, r.Error!);
        }
        var a = await _assignments.GetFullAsync(id);
        if (a is null) return NotFound();
        if (a.HunterId != UserId) return Forbid();
        return View(BuildSubmitModel(a, model));
    }

    private static SubmitWorkModel BuildSubmitModel(Models.TaskAssignment a, SubmitWorkModel m)
    {
        m.AssignmentId = a.Id;
        m.JobTitle = a.Job.Title;
        m.SubmissionInstructions = a.Job.SubmissionInstructions;
        m.IsChallenge = a.IsChallengeParticipant;
        m.LatestFeedback = a.Submissions.OrderByDescending(s => s.Version).FirstOrDefault()?.EmployerFeedback;
        return m;
    }

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> Approve(ReviewDecisionModel model, int assignmentId, int? jobId) =>
        Flash(await _assignments.ApproveAsync(UserId, model.SubmissionId, model.Feedback), "Work approved. The job is complete.",
            jobId.HasValue ? RedirectToAction(nameof(Challenge), new { id = jobId }) : RedirectToAction(nameof(Details), new { id = assignmentId }));

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> RequestRevision(ReviewDecisionModel model, int assignmentId) =>
        Flash(await _assignments.RequestRevisionAsync(UserId, model.SubmissionId, model.Feedback), "Revision requested. The Hunter has been notified.",
            RedirectToAction(nameof(Details), new { id = assignmentId }));

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> RejectChallengeSubmission(ReviewDecisionModel model, int jobId) =>
        Flash(await _assignments.RejectChallengeSubmissionAsync(UserId, model.SubmissionId, model.Feedback), "Submission marked as failed review. The next submission can now be reviewed.",
            RedirectToAction(nameof(Challenge), new { id = jobId }));

    /// <summary>Employer's challenge review board: all participants' submissions in server-timestamp order.</summary>
    [Authorize(Roles = AppRoles.Employer + "," + AppRoles.Admin)]
    public async Task<IActionResult> Challenge(int id)
    {
        var r = await _assignments.ChallengeReviewAsync(UserId, IsAdmin, id);
        if (!r.Succeeded) return r.Error == "Not authorized." ? Forbid() : NotFound();
        return View(r.Value);
    }

    public async Task<IActionResult> SubmissionFile(int id)
    {
        var s = await _assignments.GetSubmissionForDownloadAsync(UserId, IsAdmin, id);
        if (s?.FilePath is null) return NotFound();
        var file = _files.Open(s.FilePath);
        if (file is null) return NotFound();
        return File(file.Value.Stream, file.Value.ContentType, s.FileOriginalName ?? "submission");
    }
}
