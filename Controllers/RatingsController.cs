using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

[Authorize]
public class RatingsController : AppControllerBase
{
    private readonly RatingService _ratings;
    private readonly AssignmentService _assignments;

    public RatingsController(RatingService ratings, AssignmentService assignments)
    {
        _ratings = ratings;
        _assignments = assignments;
    }

    public async Task<IActionResult> Create(int id)
    {
        var a = await _assignments.GetFullAsync(id);
        if (a is null) return NotFound();
        var check = RatingService.CheckEligibility(a, UserId, a.Ratings);
        if (!check.Succeeded)
        {
            Error(check.Error!);
            return RedirectToAction("Details", "Assignments", new { id });
        }
        var rateeName = check.Value == a.HunterId ? a.Hunter.FullName : (a.Job.Employer.CompanyName ?? a.Job.Employer.FullName);
        return View(new RatingFormModel { AssignmentId = id, RateeName = rateeName, JobTitle = a.Job.Title });
    }

    [HttpPost]
    public async Task<IActionResult> Create(int id, RatingFormModel model)
    {
        if (!ModelState.IsValid)
        {
            var a = await _assignments.GetFullAsync(id);
            if (a is null) return NotFound();
            model.JobTitle = a.Job.Title;
            return View(model);
        }
        return Flash(await _ratings.CreateAsync(UserId, id, model.Stars, model.Comment), "Thank you — your rating was saved.",
            RedirectToAction("Details", "Assignments", new { id }));
    }
}
