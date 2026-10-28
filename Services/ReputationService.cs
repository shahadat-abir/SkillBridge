using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Models.Enums;

namespace SkillBridge.Services;

public record ReputationSummary(double? AverageRating, int RatingCount, int CompletedJobs);

/// <summary>
/// Performance reputation derived ONLY from genuine records: ratings left on completed assignments
/// and verified completions. Nothing is stored on the user, so it cannot be edited by the user.
/// </summary>
public class ReputationService
{
    private readonly ApplicationDbContext _db;

    public ReputationService(ApplicationDbContext db) => _db = db;

    public async Task<Dictionary<string, ReputationSummary>> ForUsersAsync(IReadOnlyCollection<string> userIds)
    {
        var ratings = await _db.Ratings.AsNoTracking()
            .Where(r => userIds.Contains(r.RateeId))
            .GroupBy(r => r.RateeId)
            .Select(g => new { UserId = g.Key, Avg = g.Average(r => (double)r.Stars), Count = g.Count() })
            .ToListAsync();
        var completed = await _db.TaskAssignments.AsNoTracking()
            .Where(a => userIds.Contains(a.HunterId) && a.Status == AssignmentStatus.Completed)
            .GroupBy(a => a.HunterId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        return userIds.Distinct().ToDictionary(id => id, id =>
        {
            var r = ratings.FirstOrDefault(x => x.UserId == id);
            var c = completed.FirstOrDefault(x => x.UserId == id);
            return new ReputationSummary(r == null ? null : Math.Round(r.Avg, 2), r?.Count ?? 0, c?.Count ?? 0);
        });
    }

    public async Task<ReputationSummary> ForUserAsync(string userId) =>
        (await ForUsersAsync(new[] { userId }))[userId];
}
