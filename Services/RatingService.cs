using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.Services;

/// <summary>Performance ratings: only parties of a COMPLETED assignment, once each, 1-5 stars.</summary>
public class RatingService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;

    public RatingService(ApplicationDbContext db, TimeProvider clock, NotificationService notifications, AuditService audit)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
        _audit = audit;
    }

    /// <summary>Returns the user to be rated, or an error explaining why rating is not allowed.</summary>
    public static ServiceResult<string> CheckEligibility(TaskAssignment a, string userId, IEnumerable<Rating> existing)
    {
        if (a.Status != AssignmentStatus.Completed) return ServiceResult<string>.Fail("Ratings can only be left after the work is completed.");
        string ratee;
        if (a.Job.EmployerId == userId) ratee = a.HunterId;
        else if (a.HunterId == userId) ratee = a.Job.EmployerId;
        else return ServiceResult<string>.Fail("Only the employer and the Hunter of this assignment can leave ratings.");
        if (existing.Any(r => r.RaterId == userId)) return ServiceResult<string>.Fail("You have already rated this assignment.");
        return ServiceResult<string>.Ok(ratee);
    }

    public async Task<ServiceResult> CreateAsync(string userId, int assignmentId, int stars, string? comment)
    {
        if (stars is < 1 or > 5) return ServiceResult.Fail("Rating must be between 1 and 5 stars.");
        var a = await _db.TaskAssignments.Include(x => x.Job).Include(x => x.Ratings).FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (a is null) return ServiceResult.Fail("Assignment not found.");
        var check = CheckEligibility(a, userId, a.Ratings);
        if (!check.Succeeded) return check;

        _db.Ratings.Add(new Rating
        {
            TaskAssignmentId = a.Id,
            RaterId = userId,
            RateeId = check.Value!,
            Stars = stars,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        });
        _notifications.Add(check.Value!, NotificationType.RatingReceived, "New rating received",
            $"You received a {stars}-star rating for \"{a.Job.Title}\".", a.JobId, a.Id, "/Profile/Me");
        _audit.Add(userId, "Rating.Create", nameof(Rating), a.Id, $"{stars} stars");
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("You have already rated this assignment.");
        }
        return ServiceResult.Ok();
    }

    public Task<List<Rating>> ReceivedByAsync(string userId, int take = 20) =>
        _db.Ratings.AsNoTracking().Include(r => r.Rater).Include(r => r.TaskAssignment).ThenInclude(t => t.Job)
            .Where(r => r.RateeId == userId).OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync();
}
