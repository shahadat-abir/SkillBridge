using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Services;

namespace SkillBridge.Controllers;

[Authorize]
public class NotificationsController : AppControllerBase
{
    private readonly NotificationService _notifications;

    public NotificationsController(NotificationService notifications) => _notifications = notifications;

    public async Task<IActionResult> Index(int page = 1) => View(await _notifications.ListAsync(UserId, page));

    /// <summary>Marks the user's own notification read and follows its (local) link.</summary>
    [HttpPost]
    public async Task<IActionResult> Open(int id)
    {
        var link = await _notifications.MarkReadAsync(UserId, id);
        return !string.IsNullOrEmpty(link) && Url.IsLocalUrl(link) ? LocalRedirect(link) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(UserId);
        Success("All notifications marked as read.");
        return RedirectToAction(nameof(Index));
    }
}
