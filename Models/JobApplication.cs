using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>A Hunter's application / bid for a standard job.</summary>
public class JobApplication
{
    public int Id { get; set; }

    public int JobId { get; set; }
    public Job Job { get; set; } = null!;

    [Required]
    public string HunterId { get; set; } = string.Empty;
    public ApplicationUser Hunter { get; set; } = null!;

    [Required, StringLength(3000)]
    public string CoverLetter { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProposedAmount { get; set; }

    public int ProposedDays { get; set; }

    [StringLength(2000)]
    public string? RelevantExperience { get; set; }

    [StringLength(300)]
    public string? AttachmentPath { get; set; }

    [StringLength(260)]
    public string? AttachmentOriginalName { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

    [StringLength(500)]
    public string? EmployerNote { get; set; }

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
