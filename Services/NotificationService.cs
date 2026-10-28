using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.ViewModels;

namespace SkillBridge.Services;

/// <summary>Database-backed notification centre (no email/SMS).</summary>
public class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public NotificationService(ApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>Queues a notification in the current unit of work (caller saves).</summary>
    public void Add(string userId, NotificationType type, string title, string message,
        int? jobId = null, int? assignmentId = null, string? link = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title.Length > 150 ? title[..150] : title,
            Message = message.Length > 1000 ? message[..1000] : message,
            JobId = jobId,
            TaskAssignmentId = assignmentId,
            LinkUrl = link,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        });
    }

    public Task<int> UnreadCountAsync(string userId) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task<PagedResult<Notification>> ListAsync(string userId, int page, int pageSize = 20)
    {
        page = Math.Max(1, page);
        var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<Notification> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    /// <summary>Marks one of the user's own notifications as read; returns its link.</summary>
    public async Task<string?> MarkReadAsync(string userId, int id)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n is null) return null;
        n.IsRead = true;
        await _db.SaveChangesAsync();
        return n.LinkUrl;
    }

    public async Task MarkAllReadAsync(string userId)
    {
        var unread = await _db.Notifications.Where(x => x.UserId == userId && !x.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync();
    }
}
