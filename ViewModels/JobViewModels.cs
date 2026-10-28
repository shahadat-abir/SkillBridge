using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SkillBridge.Domain;
using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.ViewModels;

public class JobSearchQuery
{
    public string? Keyword { get; set; }
    public int? CategoryId { get; set; }
    public int? SkillId { get; set; }
    public JobDifficulty? Difficulty { get; set; }
    public JobMode? Mode { get; set; }
    [Range(0, double.MaxValue)] public decimal? MinBudget { get; set; }
    [Range(0, double.MaxValue)] public decimal? MaxBudget { get; set; }
    /// <summary>Null = only jobs currently accepting applications.</summary>
    public JobStatus? Status { get; set; }
    public bool AllStatuses { get; set; }
    /// <summary>newest | deadline | budget_desc | budget_asc | difficulty</summary>
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
}

public class JobListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public JobDifficulty Difficulty { get; set; }
    public JobMode Mode { get; set; }
    public decimal Budget { get; set; }
    public string Currency { get; set; } = "BDT";
    public PaymentType PaymentType { get; set; }
    public DateTime ApplicationDeadline { get; set; }
    public DateTime ProjectDeadline { get; set; }
    public JobStatus Status { get; set; }
    public List<string> Skills { get; set; } = new();
    public int ApplicationsCount { get; set; }
    public int MaxParticipants { get; set; }
    public string EmployerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsAccepting { get; set; }
    public bool IsDemoData { get; set; }
}

public class JobSearchViewModel
{
    public JobSearchQuery Query { get; set; } = new();
    public PagedResult<JobListItemViewModel> Results { get; set; } = new();
    public SelectList Categories { get; set; } = null!;
    public SelectList Skills { get; set; } = null!;
}

public class RequiredSkillInput
{
    public int SkillId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Selected { get; set; }
    public SkillLevel MinimumLevel { get; set; } = SkillLevel.Intermediate;
}

public class JobFormModel
{
    public int? Id { get; set; }

    [Required, StringLength(150, MinimumLength = 5), Display(Name = "Job / task title")]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(5000, MinimumLength = 20), Display(Name = "Detailed description")]
    public string Description { get; set; } = string.Empty;

    [Required, Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Difficulty (star category)")]
    public JobDifficulty Difficulty { get; set; } = JobDifficulty.OneStar;

    [Display(Name = "Job mode")]
    public JobMode Mode { get; set; } = JobMode.Standard;

    [Range(1, 10_000_000), Display(Name = "Budget / reward amount (BDT)")]
    public decimal Budget { get; set; }

    [Display(Name = "Payment type")]
    public PaymentType PaymentType { get; set; } = PaymentType.FixedPrice;

    [Range(1, 365), Display(Name = "Expected completion time (days)")]
    public int ExpectedCompletionDays { get; set; } = 7;

    [Required, DataType(DataType.DateTime), Display(Name = "Application deadline")]
    public DateTime ApplicationDeadline { get; set; } = DateTime.Now.Date.AddDays(7).AddHours(23).AddMinutes(59);

    [Required, DataType(DataType.DateTime), Display(Name = "Project (work) deadline")]
    public DateTime ProjectDeadline { get; set; } = DateTime.Now.Date.AddDays(21).AddHours(23).AddMinutes(59);

    [Range(1, JobRules.MaxChallengeParticipants), Display(Name = "Number of participants (challenge mode)")]
    public int MaxParticipants { get; set; } = 3;

    [StringLength(2000), Display(Name = "Eligibility criteria / qualifications")]
    public string? EligibilityCriteria { get; set; }

    [StringLength(2000), Display(Name = "Submission instructions")]
    public string? SubmissionInstructions { get; set; }

    [StringLength(2000), Display(Name = "Additional terms and conditions")]
    public string? Terms { get; set; }

    [Display(Name = "Attachment / reference material")]
    public IFormFile? Attachment { get; set; }

    public string? ExistingAttachmentName { get; set; }
    public bool RemoveAttachment { get; set; }

    public List<RequiredSkillInput> RequiredSkills { get; set; } = new();
}

public class JobDetailsViewModel
{
    public Job Job { get; set; } = null!;
    public bool IsOwner { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsHunter { get; set; }
    public bool IsAccepting { get; set; }
    public JobApplication? MyApplication { get; set; }
    public TaskAssignment? MyAssignment { get; set; }
    public bool CanJoinChallenge { get; set; }
    public string? JoinBlockedReason { get; set; }
    public int ActiveParticipants { get; set; }
    public int ApplicationsCount { get; set; }
    public SkillMatchResult? MyMatch { get; set; }
    public TaskAssignment? CurrentAssignment { get; set; }
}
