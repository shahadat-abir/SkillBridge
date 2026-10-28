using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Authorization;
using SkillBridge.Models.Enums;
using SkillBridge.Services;
using SkillBridge.ViewModels;

namespace SkillBridge.Controllers;

/// <summary>Simulated payment ledger (no real money transfer).</summary>
[Authorize]
public class PaymentsController : AppControllerBase
{
    private readonly PaymentService _payments;

    public PaymentsController(PaymentService payments) => _payments = payments;

    public async Task<IActionResult> Index() => View(await _payments.ListForUserAsync(UserId, IsAdmin));

    public async Task<IActionResult> Details(int id)
    {
        var p = await _payments.GetAsync(id);
        if (p is null) return NotFound();
        if (!PaymentService.CanView(p, UserId, IsAdmin)) return Forbid();
        return View(p);
    }

    [Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var p = await _payments.GetAsync(id);
        if (p is null) return NotFound();
        if (p.EmployerId != UserId) return Forbid();
        if (p.Status != PaymentStatus.Approved)
        {
            Error("Only approved payments can be marked as paid.");
            return RedirectToAction(nameof(Details), new { id });
        }
        ViewBag.Payment = p;
        return View(new MarkPaidModel { PaymentId = id });
    }

    [HttpPost, Authorize(Roles = AppRoles.Employer)]
    public async Task<IActionResult> MarkPaid(int id, MarkPaidModel model)
    {
        if (!ModelState.IsValid || model.Method is null)
        {
            ViewBag.Payment = await _payments.GetAsync(id);
            return View(model);
        }
        return Flash(await _payments.MarkPaidAsync(UserId, id, model.Method.Value, model.ExternalReference, model.Note),
            "Payment recorded in the ledger (simulated).", RedirectToAction(nameof(Details), new { id }));
    }

    [HttpPost, Authorize(Roles = AppRoles.Employer + "," + AppRoles.Admin)]
    public async Task<IActionResult> MarkFailed(int id, string? note) =>
        Flash(await _payments.MarkFailedAsync(UserId, IsAdmin, id, note), "Payment marked as failed.", RedirectToAction(nameof(Details), new { id }));

    [HttpPost, Authorize(Roles = AppRoles.Employer + "," + AppRoles.Admin)]
    public async Task<IActionResult> Retry(int id) =>
        Flash(await _payments.RetryAsync(UserId, IsAdmin, id), "Payment is due again.", RedirectToAction(nameof(Details), new { id }));

    [HttpPost, Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Refund(int id, string? note) =>
        Flash(await _payments.RefundAsync(UserId, id, note), "Refund recorded.", RedirectToAction(nameof(Details), new { id }));
}
