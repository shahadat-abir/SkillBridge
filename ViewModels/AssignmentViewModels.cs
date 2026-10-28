using System.ComponentModel.DataAnnotations;
using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.ViewModels;

public class SubmitWorkModel
{
    public int AssignmentId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string? SubmissionInstructions { get; set; }
    public string? LatestFeedback { get; set; }
    public bool IsChallenge { get; set; }

    [Required, StringLength(4000, MinimumLength = 10), Display(Name = "Work description")]
    public string Description { get; set; } = string.Empty;

    [Url, StringLength(500), Display(Name = "Link to the work (GitHub, Drive, etc.)")]
    public string? ExternalLink { get; set; }

    [Display(Name = "File (PDF, DOCX, ZIP, PNG, JPG, TXT — max 10 MB)")]
    public IFormFile? File { get; set; }
}

public class ReviewDecisionModel
{
    public int SubmissionId { get; set; }

    [StringLength(2000)]
    public string? Feedback { get; set; }
}

public class AssignmentDetailsViewModel
{
    public TaskAssignment Assignment { get; set; } = null!;
    public bool IsHunter { get; set; }
    public bool IsEmployer { get; set; }
    public bool IsAdmin { get; set; }
    public int? NextReviewableSubmissionId { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanRate { get; set; }
    public Rating? MyRating { get; set; }
    public List<Rating> Ratings { get; set; } = new();
    public bool IsLate { get; set; }
}

public class ChallengeReviewViewModel
{
    public Job Job { get; set; } = null!;
    public List<TaskAssignment> Participants { get; set; } = new();
    public List<WorkSubmission> Submissions { get; set; } = new();   // ordered by server timestamp
    public int? NextReviewableSubmissionId { get; set; }
}

public class AssignmentListViewModel
{
    public List<TaskAssignment> Assignments { get; set; } = new();
    public bool ForEmployer { get; set; }
}

public class RatingFormModel
{
    public int AssignmentId { get; set; }
    public string RateeName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Choose between 1 and 5 stars.")]
    public int Stars { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }
}

public class MarkPaidModel
{
    public int PaymentId { get; set; }

    [Required, Display(Name = "Method")]
    public PaymentMethod? Method { get; set; }

    [StringLength(100), Display(Name = "Off-platform reference (optional, e.g. bKash TrxID you received)")]
    public string? ExternalReference { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
