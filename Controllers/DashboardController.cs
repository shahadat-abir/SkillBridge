using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Services;

namespace SkillBridge.Controllers;

[Authorize]
public class DashboardController : AppControllerBase
{
    private readonly DashboardService _dashboards;

    public DashboardController(DashboardService dashboards) => _dashboards = dashboards;

    public async Task<IActionResult> Index()
    {
        if (IsAdmin) return RedirectToAction("Index", "Admin");
        if (IsEmployer) return View("Employer", await _dashboards.EmployerAsync(UserId));
        if (IsHunter) return View("Hunter", await _dashboards.HunterAsync(UserId));
        return RedirectToAction("Index", "Home");
    }
}
