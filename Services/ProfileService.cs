using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Models;
using SkillBridge.Models.Enums;
using SkillBridge.ViewModels;

namespace SkillBridge.Services;

public class ProfileService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public ProfileService(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public Task<List<UserSkill>> SkillsAsync(string userId) =>
        _db.UserSkills.AsNoTracking().Include(s => s.Skill).Where(s => s.UserId == userId).OrderBy(s => s.Skill.Name).ToListAsync();

    public async Task<ServiceResult> UpdateAsync(string userId, ProfileEditModel m)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return ServiceResult.Fail("User not found.");
        user.FullName = m.FullName.Trim();
        user.Headline = m.Headline?.Trim();
        user.Bio = m.Bio?.Trim();
        user.Location = m.Location?.Trim();
        user.Education = m.Education?.Trim();
        user.Experience = m.Experience?.Trim();
        user.PortfolioUrl = m.PortfolioUrl?.Trim();
        user.Availability = m.Availability?.Trim();
        user.CompanyName = m.CompanyName?.Trim();
        _audit.Add(userId, "Profile.Update", nameof(ApplicationUser), userId);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AddOrUpdateSkillAsync(string userId, AddSkillModel m)
    {
        if (!Enum.IsDefined(m.Level)) return ServiceResult.Fail("Invalid skill level.");
        if (m.YearsOfExperience is < 0 or > 50) return ServiceResult.Fail("Years of experience must be between 0 and 50.");
        var skill = await _db.Skills.FirstOrDefaultAsync(s => s.Id == m.SkillId && s.IsActive);
        if (skill is null) return ServiceResult.Fail("Please choose a valid skill.");
        var existing = await _db.UserSkills.FirstOrDefaultAsync(s => s.UserId == userId && s.SkillId == m.SkillId);
        if (existing is null)
            _db.UserSkills.Add(new UserSkill { UserId = userId, SkillId = m.SkillId, Level = m.Level, YearsOfExperience = m.YearsOfExperience });
        else
        {
            existing.Level = m.Level;
            existing.YearsOfExperience = m.YearsOfExperience;
        }
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveSkillAsync(string userId, int skillId)
    {
        var existing = await _db.UserSkills.FirstOrDefaultAsync(s => s.UserId == userId && s.SkillId == skillId);
        if (existing is null) return ServiceResult.Fail("Skill not found on your profile.");
        _db.UserSkills.Remove(existing);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public Task<List<TaskAssignment>> CompletedWorkAsync(string hunterId, int take = 10) =>
        _db.TaskAssignments.AsNoTracking().Include(a => a.Job).ThenInclude(j => j.Category)
            .Where(a => a.HunterId == hunterId && a.Status == AssignmentStatus.Completed)
            .OrderByDescending(a => a.CompletedAt).Take(take).ToListAsync();
}
