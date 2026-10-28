using SkillBridge.Models.Enums;

namespace SkillBridge.Domain;

public record RequiredSkill(int SkillId, string Name, SkillLevel MinimumLevel);
public record HunterSkillLevel(int SkillId, SkillLevel Level, int YearsOfExperience);

public record SkillMatchLine(int SkillId, string Name, SkillLevel Required, SkillLevel? HunterLevel, int? Years)
{
    public bool HasSkill => HunterLevel.HasValue;
    public bool MeetsLevel => HunterLevel.HasValue && HunterLevel.Value >= Required;
}

public record SkillMatchResult(IReadOnlyList<SkillMatchLine> Lines)
{
    public int Required => Lines.Count;
    public int Met => Lines.Count(l => l.MeetsLevel);
    public int Partial => Lines.Count(l => l.HasSkill && !l.MeetsLevel);
    /// <summary>Percentage of required skills met at the required level (0-100). 100 when the job lists none.</summary>
    public int Percent => Required == 0 ? 100 : (int)Math.Round(100.0 * Met / Required);
}

/// <summary>
/// Compares a job's required skills with a Hunter's self-reported skills.
/// Transparent: the Employer sees every line, not only the percentage.
/// </summary>
public static class SkillMatcher
{
    public static SkillMatchResult Match(IEnumerable<RequiredSkill> required, IEnumerable<HunterSkillLevel> hunterSkills)
    {
        var bySkill = hunterSkills.GroupBy(h => h.SkillId).ToDictionary(g => g.Key, g => g.First());
        var lines = required
            .Select(r => bySkill.TryGetValue(r.SkillId, out var h)
                ? new SkillMatchLine(r.SkillId, r.Name, r.MinimumLevel, h.Level, h.YearsOfExperience)
                : new SkillMatchLine(r.SkillId, r.Name, r.MinimumLevel, null, null))
            .ToList();
        return new SkillMatchResult(lines);
    }
}
