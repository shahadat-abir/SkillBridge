using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Authorization;
using SkillBridge.Services;

namespace SkillBridge.Controllers;

/// <summary>Shared helpers. Authorization is enforced with [Authorize] attributes AND service-level ownership checks.</summary>
public abstract class AppControllerBase : Controller
{
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    protected bool IsAdmin => User.IsInRole(AppRoles.Admin);
    protected bool IsEmployer => User.IsInRole(AppRoles.Employer);
    protected bool IsHunter => User.IsInRole(AppRoles.Hunter);

    protected void Success(string message) => TempData["Success"] = message;
    protected void Error(string message) => TempData["Error"] = message;

    /// <summary>Shows the result as a flash message and redirects.</summary>
    protected IActionResult Flash(ServiceResult result, string successMessage, IActionResult redirect)
    {
        if (result.Succeeded) Success(successMessage);
        else Error(result.Error ?? "The action could not be completed.");
        return redirect;
    }
}
