using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>
/// A Hunter working on a job (JobAssignment).
/// Standard mode: created when the Employer selects an application (one active per job).
/// Challenge mode: one per registered participant (ChallengeParticipant), up to Job.MaxParticipants.
/// </summary>
public class TaskAssignment
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public Job Job { get; set; } = null!;

    [Required]
    public string HunterId { get; set; } = string.Empty;
    public ApplicationUser Hunter { get; set; } = null!;

    /// <summary>Selected application (standard mode only).</summary>
    public int? ApplicationId { get; set; }
    public JobApplication? Application { get; set; }

    public bool IsChallengeParticipant { get; set; }

    /// <summary>Challenge mode: the participant whose submission won.</summary>
    public bool IsWinner { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AgreedAmount { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime DueDate { get; set; }

    public AssignmentStatus Status { get; set; } = AssignmentStatus.PendingAcceptance;

    // Navigation
    public ICollection<WorkSubmission> Submissions { get; set; } = new List<WorkSubmission>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public Payment? Payment { get; set; }
}
