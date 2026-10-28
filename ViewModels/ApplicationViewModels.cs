using System.ComponentModel.DataAnnotations;
using SkillBridge.Domain;
using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.ViewModels;

public class ApplyFormModel
{
    public int JobId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public decimal JobBudget { get; set; }
    public int JobExpectedDays { get; set; }
    public PaymentType PaymentType { get; set; }

    [Required, StringLength(3000, MinimumLength = 30), Display(Name = "Cover letter / proposal")]
    public string CoverLetter { get; set; } = string.Empty;

    [Range(1, 10_000_000), Display(Name = "Proposed price (BDT)")]
    public decimal ProposedAmount { get; set; }

    [Range(1, 365), Display(Name = "Proposed completion time (days)")]
    public int ProposedDays { get; set; }

    [StringLength(2000), Display(Name = "Relevant skills / experience for this job")]
    public string? RelevantExperience { get; set; }

    [Display(Name = "Supporting document (optional)")]
    public IFormFile? Attachment { get; set; }
}

public class ApplicantComparisonRow
{
    public int ApplicationId { get; set; }
    public string HunterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Headline { get; set; }
    public ApplicationStatus Status { get; set; }
    public decimal ProposedAmount { get; set; }
    public int ProposedDays { get; set; }
    public DateTime AppliedAt { get; set; }
    public SkillMatchResult Match { get; set; } = new(Array.Empty<SkillMatchLine>());
    public double? AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int CompletedJobs { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? Education { get; set; }
    public string? Experience { get; set; }
    public string? RelevantExperience { get; set; }
    public string? Availability { get; set; }
    public string CoverLetter { get; set; } = string.Empty;
    public bool HasAttachment { get; set; }
    public bool WithinBudget { get; set; }
    public bool WithinExpectedTime { get; set; }
    public bool IsLowestPrice { get; set; }
    public bool IsFastest { get; set; }
    public bool IsHighestRated { get; set; }
    public bool IsBestSkillMatch { get; set; }
}

public class ComparisonViewModel
{
    public Job Job { get; set; } = null!;
    public List<ApplicantComparisonRow> Rows { get; set; } = new();
    public string Sort { get; set; } = "applied";
    public bool CanSelect { get; set; }
    public bool HasActiveAssignment { get; set; }
    public List<int> SelectedIds { get; set; } = new();
}

public class ApplicationsForJobViewModel
{
    public Job Job { get; set; } = null!;
    public List<JobApplication> Applications { get; set; } = new();
    public bool CanSelect { get; set; }
}
