using System.ComponentModel.DataAnnotations;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>In-app (database-backed) notification. No email/SMS delivery.</summary>
public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public NotificationType Type { get; set; } = NotificationType.General;

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    public int? JobId { get; set; }
    public int? TaskAssignmentId { get; set; }

    [StringLength(300)]
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
