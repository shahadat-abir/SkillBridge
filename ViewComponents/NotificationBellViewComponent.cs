using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Services;

namespace SkillBridge.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly NotificationService _notifications;

    public NotificationBellViewComponent(NotificationService notifications) => _notifications = notifications;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var count = userId is null ? 0 : await _notifications.UnreadCountAsync(userId);
        return View(count);
    }
}
