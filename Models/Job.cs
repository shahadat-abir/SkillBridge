using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>A job/task posted by an Employer.</summary>
public class Job
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(5000)]
    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public JobCategory Category { get; set; } = null!;

    /// <summary>Complexity of the work (star category). Not a Hunter skill level.</summary>
    public JobDifficulty Difficulty { get; set; } = JobDifficulty.OneStar;

    public JobMode Mode { get; set; } = JobMode.Standard;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Budget { get; set; }

    [StringLength(3)]
    public string Currency { get; set; } = "BDT";

    public PaymentType PaymentType { get; set; } = PaymentType.FixedPrice;

    /// <summary>Expected number of days to complete the work once started.</summary>
    public int ExpectedCompletionDays { get; set; }

    /// <summary>Last moment (UTC) applications / challenge registrations are accepted.</summary>
    public DateTime ApplicationDeadline { get; set; }

    /// <summary>Last moment (UTC) the work itself must be delivered.</summary>
    public DateTime ProjectDeadline { get; set; }

    /// <summary>Standard mode: 1. Challenge mode: maximum number of participants.</summary>
    public int MaxParticipants { get; set; } = 1;

    [StringLength(2000)]
    public string? EligibilityCriteria { get; set; }   // qualifications

    [StringLength(2000)]
    public string? SubmissionInstructions { get; set; }

    [StringLength(2000)]
    public string? Terms { get; set; }

    [StringLength(300)]
    public string? AttachmentPath { get; set; }

    [StringLength(260)]
    public string? AttachmentOriginalName { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string EmployerId { get; set; } = string.Empty;
    public ApplicationUser Employer { get; set; } = null!;

    /// <summary>Challenge mode: the winning assignment (set once, under concurrency control).</summary>
    public int? WinnerAssignmentId { get; set; }

    /// <summary>Optimistic concurrency token; changed on every state transition to prevent conflicting selections/winners.</summary>
    [ConcurrencyCheck]
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public bool IsDemoData { get; set; }

    // Navigation
    public ICollection<JobSkill> RequiredSkills { get; set; } = new List<JobSkill>();
    public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
}
