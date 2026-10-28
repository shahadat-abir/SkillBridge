using System.ComponentModel.DataAnnotations;

namespace SkillBridge.Models;

/// <summary>Skill catalogue entry managed by the Admin.</summary>
public class Skill
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(60)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    public ICollection<JobSkill> JobSkills { get; set; } = new List<JobSkill>();
}
