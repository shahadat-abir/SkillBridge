using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

public class HomeController : AppControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly JobService _jobs;

    public HomeController(ApplicationDbContext db, JobService jobs)
    {
        _db = db;
        _jobs = jobs;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.OpenJobs = await _db.Jobs.CountAsync(j => j.Status == JobStatus.Open && j.ApplicationDeadline > DateTime.UtcNow);
        ViewBag.CompletedJobs = await _db.Jobs.CountAsync(j => j.Status == JobStatus.Completed);
        ViewBag.Categories = await _db.JobCategories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        var latest = await _jobs.SearchAsync(new JobSearchQuery { Page = 1 });
        return View(latest.Items.Take(3).ToList());
    }

    public IActionResult About() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [Route("Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        ViewBag.Code = code;
        return View("StatusCode");
    }
}
