using Microsoft.EntityFrameworkCore;
using SkillBridge.Data;
using SkillBridge.Domain;
using SkillBridge.Models;
using SkillBridge.Models.Enums;

namespace SkillBridge.Services;

/// <summary>
/// SIMULATED payment ledger. Tracks payment states and an auditable history.
/// No payment gateway is integrated and no money is transferred.
/// </summary>
public class PaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;

    public PaymentService(ApplicationDbContext db, TimeProvider clock, NotificationService notifications, AuditService audit)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
        _audit = audit;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    /// <summary>Creates a ledger entry for an assignment (caller saves).</summary>
    public Payment CreateFor(TaskAssignment assignment, Job job, PaymentStatus initialStatus, string? actorId)
    {
        var payment = new Payment
        {
            TaskAssignment = assignment,
            EmployerId = job.EmployerId,
            HunterId = assignment.HunterId,
            Amount = assignment.AgreedAmount,
            Currency = job.Currency,
            Status = PaymentStatus.Pending,
            LedgerReference = $"SIM-{Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            CreatedAt = Now,
            UpdatedAt = Now
        };
        payment.History.Add(new PaymentEvent { FromStatus = null, ToStatus = PaymentStatus.Pending, ChangedById = actorId, Note = "Ledger entry created", CreatedAt = Now });
        _db.Payments.Add(payment);
        if (initialStatus != PaymentStatus.Pending)
            Transition(payment, initialStatus, actorId, "Initial status");
        return payment;
    }

    /// <summary>Applies a validated status change and records history (caller saves).</summary>
    public bool Transition(Payment payment, PaymentStatus to, string? actorId, string? note)
    {
        if (payment.Status == to) return true;
        if (!PaymentRules.CanTransition(payment.Status, to)) return false;
        payment.History.Add(new PaymentEvent { FromStatus = payment.Status, ToStatus = to, ChangedById = actorId, Note = note, CreatedAt = Now });
        payment.Status = to;
        payment.UpdatedAt = Now;
        if (to == PaymentStatus.Paid) payment.PaidAt = Now;
        return true;
    }

    public Task<Payment?> GetAsync(int id) =>
        _db.Payments
            .Include(p => p.TaskAssignment).ThenInclude(t => t.Job)
            .Include(p => p.Employer).Include(p => p.Hunter)
            .Include(p => p.History.OrderBy(h => h.CreatedAt))
            .FirstOrDefaultAsync(p => p.Id == id);

    public static bool CanView(Payment p, string userId, bool isAdmin) => isAdmin || p.EmployerId == userId || p.HunterId == userId;

    public async Task<List<Payment>> ListForUserAsync(string userId, bool isAdmin)
    {
        var q = _db.Payments.AsNoTracking()
            .Include(p => p.TaskAssignment).ThenInclude(t => t.Job)
            .Include(p => p.Employer).Include(p => p.Hunter)
            .AsQueryable();
        if (!isAdmin) q = q.Where(p => p.EmployerId == userId || p.HunterId == userId);
        return await q.OrderByDescending(p => p.UpdatedAt).Take(500).ToListAsync();
    }

    /// <summary>Employer records that they paid the Hunter off-platform (simulated ledger only).</summary>
    public async Task<ServiceResult> MarkPaidAsync(string employerId, int paymentId, PaymentMethod method, string? externalRef, string? note)
    {
        var p = await _db.Payments.Include(x => x.TaskAssignment).ThenInclude(t => t.Job).FirstOrDefaultAsync(x => x.Id == paymentId);
        if (p is null) return ServiceResult.Fail("Payment not found.");
        if (p.EmployerId != employerId) return ServiceResult.Fail("You can only record payments for your own jobs.");
        if (p.Status != PaymentStatus.Approved) return ServiceResult.Fail("Only payments approved after work approval can be marked as paid.");

        p.Method = method;
        p.ExternalReference = string.IsNullOrWhiteSpace(externalRef) ? null : externalRef.Trim();
        Transition(p, PaymentStatus.Paid, employerId, string.IsNullOrWhiteSpace(note) ? "Employer recorded payment (simulated)" : note);
        _notifications.Add(p.HunterId, NotificationType.PaymentUpdated, "Payment recorded",
            $"The employer recorded payment of {p.Amount:N2} {p.Currency} for \"{p.TaskAssignment.Job.Title}\" (simulated ledger — confirm receipt yourself).",
            p.TaskAssignment.JobId, p.TaskAssignmentId, $"/Payments/Details/{p.Id}");
        _audit.Add(employerId, "Payment.MarkPaid", nameof(Payment), p.Id, $"{p.LedgerReference} {method}");
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> MarkFailedAsync(string actorId, bool isAdmin, int paymentId, string? note)
    {
        var p = await _db.Payments.Include(x => x.TaskAssignment).ThenInclude(t => t.Job).FirstOrDefaultAsync(x => x.Id == paymentId);
        if (p is null) return ServiceResult.Fail("Payment not found.");
        if (!isAdmin && p.EmployerId != actorId) return ServiceResult.Fail("Not authorized.");
        if (!Transition(p, PaymentStatus.Failed, actorId, note ?? "Payment attempt failed"))
            return ServiceResult.Fail($"A {p.Status} payment cannot be marked as failed.");
        _notifications.Add(p.HunterId, NotificationType.PaymentUpdated, "Payment failed",
            $"A payment attempt for \"{p.TaskAssignment.Job.Title}\" was recorded as failed.", p.TaskAssignment.JobId, p.TaskAssignmentId, $"/Payments/Details/{p.Id}");
        _audit.Add(actorId, "Payment.Failed", nameof(Payment), p.Id, note);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RetryAsync(string actorId, bool isAdmin, int paymentId)
    {
        var p = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId);
        if (p is null) return ServiceResult.Fail("Payment not found.");
        if (!isAdmin && p.EmployerId != actorId) return ServiceResult.Fail("Not authorized.");
        if (p.Status != PaymentStatus.Failed || !Transition(p, PaymentStatus.Approved, actorId, "Payment due again after failure"))
            return ServiceResult.Fail("Only failed payments can be retried.");
        _audit.Add(actorId, "Payment.Retry", nameof(Payment), p.Id);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Admin-only refund record for a paid entry.</summary>
    public async Task<ServiceResult> RefundAsync(string adminId, int paymentId, string? note)
    {
        var p = await _db.Payments.Include(x => x.TaskAssignment).ThenInclude(t => t.Job).FirstOrDefaultAsync(x => x.Id == paymentId);
        if (p is null) return ServiceResult.Fail("Payment not found.");
        if (!Transition(p, PaymentStatus.Refunded, adminId, note ?? "Refund recorded by admin"))
            return ServiceResult.Fail("Only paid entries can be refunded.");
        _notifications.Add(p.HunterId, NotificationType.PaymentUpdated, "Payment refunded", $"The payment for \"{p.TaskAssignment.Job.Title}\" was marked refunded by an administrator.", p.TaskAssignment.JobId, p.TaskAssignmentId, $"/Payments/Details/{p.Id}");
        _notifications.Add(p.EmployerId, NotificationType.PaymentUpdated, "Payment refunded", $"The payment for \"{p.TaskAssignment.Job.Title}\" was marked refunded by an administrator.", p.TaskAssignment.JobId, p.TaskAssignmentId, $"/Payments/Details/{p.Id}");
        _audit.Add(adminId, "Payment.Refund", nameof(Payment), p.Id, note);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
