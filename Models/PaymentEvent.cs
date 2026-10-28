using System.ComponentModel.DataAnnotations;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>Audit trail of every payment status change.</summary>
public class PaymentEvent
{
    public int Id { get; set; }

    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public PaymentStatus? FromStatus { get; set; }
    public PaymentStatus ToStatus { get; set; }

    [StringLength(450)]
    public string? ChangedById { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
