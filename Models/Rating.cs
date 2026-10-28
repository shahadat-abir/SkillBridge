using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Models;

/// <summary>
/// Performance rating / review (1-5 stars) for COMPLETED work.
/// Each party of a completed assignment can review the other exactly once.
/// Separate from job difficulty and from skill level.
/// </summary>
public class Rating
{
    public int Id { get; set; }

    public int TaskAssignmentId { get; set; }
    public TaskAssignment TaskAssignment { get; set; } = null!;

    [Required]
    public string RaterId { get; set; } = string.Empty;
    public ApplicationUser Rater { get; set; } = null!;

    [Required]
    public string RateeId { get; set; } = string.Empty;
    public ApplicationUser Ratee { get; set; } = null!;

    [Range(1, 5)]
    public int Stars { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
