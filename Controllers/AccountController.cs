using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SkillBridge.Authorization;
using SkillBridge.Models;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

public class AccountController : AppControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly AuditService _audit;
    private readonly Data.ApplicationDbContext _db;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, AuditService audit, Data.ApplicationDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _audit = audit;
        _db = db;
    }

    [HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View(new RegisterViewModel());

    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!AppRoles.SelfRegistrable.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Please choose Employer or Hunter.");
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            CompanyName = model.Role == AppRoles.Employer ? model.CompanyName?.Trim() : null
        };
        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }
        await _users.AddToRoleAsync(user, model.Role);
        _audit.Add(user.Id, "Account.Register", nameof(ApplicationUser), user.Id, model.Role);
        await _db.SaveChangesAsync();
        await _signIn.SignInAsync(user, isPersistent: false);
        Success($"Welcome to SkillBridge, {user.FullName}! Your {model.Role} account is ready.");
        return model.Role == AppRoles.Hunter ? RedirectToAction("Edit", "Profile") : RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.FindByEmailAsync(model.Email.Trim());
        if (user is { IsSuspended: true })
        {
            ModelState.AddModelError(string.Empty, "This account is suspended. Please contact the administrator.");
            return View(model);
        }
        var result = await _signIn.PasswordSignInAsync(model.Email.Trim(), model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl);
            return RedirectToAction("Index", "Dashboard");
        }
        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Too many failed attempts. The account is temporarily locked; try again later."
            : "Invalid email or password.");
        return View(model);
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
