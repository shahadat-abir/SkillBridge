using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Models;

/// <summary>Append-only record of sensitive operations.</summary>
public class AuditLog
{
    public long Id { get; set; }

    [StringLength(450)]
    public string? UserId { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    [Required, StringLength(80)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string EntityType { get; set; } = string.Empty;

    [StringLength(60)]
    public string? EntityId { get; set; }

    [StringLength(1000)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
