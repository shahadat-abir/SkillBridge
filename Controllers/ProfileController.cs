using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Authorization;
using SkillBridge.Data;
using SkillBridge.Models;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

[Authorize]
public class ProfileController : AppControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ProfileService _profiles;
    private readonly ReputationService _reputation;
    private readonly RatingService _ratings;
    private readonly ApplicationDbContext _db;

    public ProfileController(UserManager<ApplicationUser> users, ProfileService profiles, ReputationService reputation, RatingService ratings, ApplicationDbContext db)
    {
        _users = users;
        _profiles = profiles;
        _reputation = reputation;
        _ratings = ratings;
        _db = db;
    }

    public IActionResult Me() => RedirectToAction(nameof(Details), new { id = UserId });

    /// <summary>Public profile (visible to signed-in users). Email is shown only to the owner and admins.</summary>
    public async Task<IActionResult> Details(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return NotFound();
        var roles = await _users.GetRolesAsync(user);
        var rep = await _reputation.ForUserAsync(id);
        var vm = new ProfileViewModel
        {
            User = user,
            IsHunter = roles.Contains(AppRoles.Hunter),
            IsEmployer = roles.Contains(AppRoles.Employer),
            IsOwnProfile = id == UserId,
            CanSeePrivate = id == UserId || IsAdmin,
            Skills = await _profiles.SkillsAsync(id),
            AverageRating = rep.AverageRating,
            RatingCount = rep.RatingCount,
            CompletedJobs = rep.CompletedJobs,
            TotalApplications = await _db.JobApplications.CountAsync(a => a.HunterId == id),
            JobsPosted = await _db.Jobs.CountAsync(j => j.EmployerId == id),
            Reviews = await _ratings.ReceivedByAsync(id),
            CompletedWork = await _profiles.CompletedWorkAsync(id)
        };
        return View(vm);
    }

    public async Task<IActionResult> Edit()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(new ProfileEditModel
        {
            FullName = user.FullName,
            Headline = user.Headline,
            Bio = user.Bio,
            Location = user.Location,
            Education = user.Education,
            Experience = user.Experience,
            PortfolioUrl = user.PortfolioUrl,
            Availability = user.Availability,
            CompanyName = user.CompanyName,
            IsHunter = IsHunter,
            IsEmployer = IsEmployer
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ProfileEditModel model)
    {
        model.IsHunter = IsHunter;
        model.IsEmployer = IsEmployer;
        if (!ModelState.IsValid) return View(model);
        var r = await _profiles.UpdateAsync(UserId, model);
        if (!r.Succeeded)
        {
            ModelState.AddModelError(string.Empty, r.Error!);
            return View(model);
        }
        Success("Profile saved.");
        return IsHunter ? RedirectToAction(nameof(Skills)) : RedirectToAction(nameof(Me));
    }

    [Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> Skills()
    {
        ViewBag.MySkills = await _profiles.SkillsAsync(UserId);
        ViewBag.SkillOptions = new SelectList(await _db.Skills.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(), "Id", "Name");
        return View(new AddSkillModel());
    }

    [HttpPost, Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> AddSkill(AddSkillModel model)
    {
        if (!ModelState.IsValid)
        {
            Error("Please choose a skill and a valid level.");
            return RedirectToAction(nameof(Skills));
        }
        return Flash(await _profiles.AddOrUpdateSkillAsync(UserId, model), "Skill saved.", RedirectToAction(nameof(Skills)));
    }

    [HttpPost, Authorize(Roles = AppRoles.Hunter)]
    public async Task<IActionResult> RemoveSkill(int skillId) =>
        Flash(await _profiles.RemoveSkillAsync(UserId, skillId), "Skill removed.", RedirectToAction(nameof(Skills)));
}
