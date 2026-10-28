using System.ComponentModel.DataAnnotations;
using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.ViewModels;

public class ProfileViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public bool IsHunter { get; set; }
    public bool IsEmployer { get; set; }
    public bool IsOwnProfile { get; set; }
    public bool CanSeePrivate { get; set; }
    public List<UserSkill> Skills { get; set; } = new();
    public double? AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int CompletedJobs { get; set; }
    public int TotalApplications { get; set; }
    public int JobsPosted { get; set; }
    public List<Rating> Reviews { get; set; } = new();
    public List<TaskAssignment> CompletedWork { get; set; } = new();
}

public class ProfileEditModel
{
    [Required, StringLength(100), Display(Name = "Display name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(150), Display(Name = "Professional introduction (headline)")]
    public string? Headline { get; set; }

    [StringLength(2000), Display(Name = "About me")]
    public string? Bio { get; set; }

    [StringLength(100)]
    public string? Location { get; set; }

    [StringLength(1000)]
    public string? Education { get; set; }

    [StringLength(2000), Display(Name = "Relevant experience")]
    public string? Experience { get; set; }

    [Url, StringLength(300), Display(Name = "Portfolio link")]
    public string? PortfolioUrl { get; set; }

    [StringLength(100)]
    public string? Availability { get; set; }

    [StringLength(150), Display(Name = "Company / organisation")]
    public string? CompanyName { get; set; }

    public bool IsHunter { get; set; }
    public bool IsEmployer { get; set; }
}

public class AddSkillModel
{
    [Required, Display(Name = "Skill")]
    public int SkillId { get; set; }

    [Display(Name = "Your level (self-reported)")]
    public SkillLevel Level { get; set; } = SkillLevel.Beginner;

    [Range(0, 50), Display(Name = "Years of experience")]
    public int YearsOfExperience { get; set; }
}
