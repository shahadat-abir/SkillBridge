using SkillBridge.Data;
using SkillBridge.Models;

namespace SkillBridge.Services;

/// <summary>Adds audit entries to the current unit of work (saved together with the business change).</summary>
public class AuditService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public AuditService(ApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public void Add(string? userId, string action, string entityType, object? entityId, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Details = details is { Length: > 1000 } ? details[..1000] : details,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        });
    }
}
