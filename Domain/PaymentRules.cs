using SkillBridge.Models.Enums;

namespace SkillBridge.Domain;

/// <summary>Allowed transitions of the simulated payment ledger.</summary>
public static class PaymentRules
{
    private static readonly Dictionary<PaymentStatus, PaymentStatus[]> Allowed = new()
    {
        [PaymentStatus.Pending]          = new[] { PaymentStatus.AwaitingApproval, PaymentStatus.Approved, PaymentStatus.Cancelled },
        [PaymentStatus.AwaitingApproval] = new[] { PaymentStatus.Pending, PaymentStatus.Approved, PaymentStatus.Cancelled },
        [PaymentStatus.Approved]         = new[] { PaymentStatus.Paid, PaymentStatus.Failed, PaymentStatus.Cancelled },
        [PaymentStatus.Failed]           = new[] { PaymentStatus.Approved, PaymentStatus.Cancelled },
        [PaymentStatus.Paid]             = new[] { PaymentStatus.Refunded },
        [PaymentStatus.Cancelled]        = Array.Empty<PaymentStatus>(),
        [PaymentStatus.Refunded]         = Array.Empty<PaymentStatus>()
    };

    public static bool CanTransition(PaymentStatus from, PaymentStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}
