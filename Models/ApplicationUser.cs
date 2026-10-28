using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SkillBridge.Models;

/// <summary>
/// A platform user. The role (Admin / Employer / Hunter) is stored with ASP.NET Core Identity roles.
/// Hunter-specific and Employer-specific profile fields live here to keep the schema simple.
/// Performance rating is NOT stored here; it is derived from Rating records.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Headline { get; set; }          // professional introduction (one line)

    [StringLength(2000)]
    public string? Bio { get; set; }

    [StringLength(100)]
    public string? Location { get; set; }

    // Hunter profile
    [StringLength(1000)]
    public string? Education { get; set; }

    [StringLength(2000)]
    public string? Experience { get; set; }

    [StringLength(300)]
    public string? PortfolioUrl { get; set; }

    [StringLength(100)]
    public string? Availability { get; set; }

    // Employer profile
    [StringLength(150)]
    public string? CompanyName { get; set; }

    public bool IsSuspended { get; set; }

    [StringLength(500)]
    public string? SuspensionReason { get; set; }

    /// <summary>True for clearly labelled demonstration accounts created by the seeder.</summary>
    public bool IsDemoAccount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<UserSkill> Skills { get; set; } = new List<UserSkill>();
    public ICollection<Job> PostedJobs { get; set; } = new List<Job>();
    public ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
    public ICollection<Rating> RatingsReceived { get; set; } = new List<Rating>();
    public ICollection<Rating> RatingsGiven { get; set; } = new List<Rating>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
