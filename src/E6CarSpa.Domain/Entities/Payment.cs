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
}
