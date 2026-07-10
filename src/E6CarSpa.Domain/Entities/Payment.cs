using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Domain.Entities;

/// <summary>A payment received against an invoice. Multiple allowed (part payments / split tender).</summary>
public class Payment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Transaction id / UPI reference / card approval code.</summary>
    public string? Reference { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public Guid? ReceivedByUserId { get; set; }

    /// <summary>
    /// When set, this row is a REVERSAL of the payment with this id (its <see cref="Amount"/> is the
    /// negative of the original). Normal payments leave this null. A payment is considered "reversed"
    /// when another payment on the same invoice points back at it here — used to refund a payment or
    /// correct a wrong entry while preserving the full history (money records are never deleted).
    /// </summary>
    public Guid? ReversalOfPaymentId { get; set; }
}
