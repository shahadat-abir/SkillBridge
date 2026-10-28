using System.ComponentModel.DataAnnotations;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>A versioned delivery of work for an assignment. History is preserved (never overwritten).</summary>
public class WorkSubmission
{
    public int Id { get; set; }

    public int TaskAssignmentId { get; set; }
    public TaskAssignment TaskAssignment { get; set; } = null!;

    /// <summary>1, 2, 3 ... per assignment.</summary>
    public int Version { get; set; }

    [Required, StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ExternalLink { get; set; }

    [StringLength(300)]
    public string? FilePath { get; set; }

    [StringLength(260)]
    public string? FileOriginalName { get; set; }

    /// <summary>Server-side timestamp (UTC). Used for first-finisher ordering in challenge mode.</summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public SubmissionStatus Status { get; set; } = SubmissionStatus.PendingReview;

    [StringLength(2000)]
    public string? EmployerFeedback { get; set; }

    public DateTime? ReviewedAt { get; set; }
}
