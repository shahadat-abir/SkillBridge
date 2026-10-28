using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>A skill required by a job with the minimum skill level (JobRequiredSkill).</summary>
public class JobSkill
{
    public int JobId { get; set; }
    public Job Job { get; set; } = null!;

    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public SkillLevel MinimumLevel { get; set; } = SkillLevel.Intermediate;
}
