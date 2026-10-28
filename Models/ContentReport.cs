using System.ComponentModel.DataAnnotations;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>A user report about a problematic job post, handled by the Admin.</summary>
public class ContentReport
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public Job Job { get; set; } = null!;

    [Required]
    public string ReporterId { get; set; } = string.Empty;
    public ApplicationUser Reporter { get; set; } = null!;

    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public ReportStatus Status { get; set; } = ReportStatus.Open;

    [StringLength(1000)]
    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
