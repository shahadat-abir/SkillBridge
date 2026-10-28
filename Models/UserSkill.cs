using System.ComponentModel.DataAnnotations;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>
/// A Hunter's skill with a skill-specific proficiency level (HunterSkill).
/// Levels are SELF-REPORTED; the platform does not verify them.
/// </summary>
public class UserSkill
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public SkillLevel Level { get; set; } = SkillLevel.Beginner;

    [Range(0, 50)]
    public int YearsOfExperience { get; set; }
}
