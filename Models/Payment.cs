using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SkillBridge.Models.Enums;

namespace SkillBridge.Models;

/// <summary>
/// SIMULATED payment ledger entry (PaymentRecord). SkillBridge tracks payment states only;
/// it does not integrate a payment gateway, store card details or transfer real money.
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int TaskAssignmentId { get; set; }
    public TaskAssignment TaskAssignment { get; set; } = null!;

    [Required]
    public string EmployerId { get; set; } = string.Empty;
    public ApplicationUser Employer { get; set; } = null!;

    [Required]
    public string HunterId { get; set; } = string.Empty;
    public ApplicationUser Hunter { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [StringLength(3)]
    public string Currency { get; set; } = "BDT";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public PaymentMethod? Method { get; set; }

    /// <summary>Internal ledger reference (prefixed "SIM-"); NOT a gateway transaction ID.</summary>
    [StringLength(40)]
    public string LedgerReference { get; set; } = string.Empty;

    /// <summary>Optional reference typed by the employer for an off-platform payment (e.g. bKash TrxID).</summary>
    [StringLength(100)]
    public string? ExternalReference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public ICollection<PaymentEvent> History { get; set; } = new List<PaymentEvent>();
}
