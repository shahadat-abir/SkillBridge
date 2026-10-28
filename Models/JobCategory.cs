using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Models;

/// <summary>Job category managed by the Admin.</summary>
public class JobCategory
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
