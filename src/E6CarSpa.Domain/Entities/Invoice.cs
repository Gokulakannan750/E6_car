using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Domain.Entities;

/// <summary>
/// The central billing document. Starts life as a Quotation when the car arrives and
/// becomes a tax Invoice when finalised — the same record, advanced through <see cref="Status"/>.
/// </summary>
public class Invoice : BaseEntity
{
    /// <summary>
    /// Human-facing number. Null while a pure draft; assigned (sequentially, no yearly reset)
    /// when the document is converted to an invoice. Format is configurable in CompanySettings.
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>Monotonic counter behind <see cref="InvoiceNumber"/>; never resets.</summary>
    public long? SequenceNumber { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Quotation;

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    // ----- Monetary breakdown (all amounts in INR) -----

    /// <summary>Sum of line totals before discount and tax.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Invoice-level discount applied on the taxable value.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Net amount on which GST is charged (SubTotal - Discount).</summary>
    public decimal TaxableValue { get; set; }

    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }

    /// <summary>Used instead of CGST+SGST for inter-state supply.</summary>
    public decimal IgstAmount { get; set; }

    public decimal TotalTax { get; set; }

    /// <summary>Final payable amount (TaxableValue + TotalTax, rounded).</summary>
    public decimal GrandTotal { get; set; }

    /// <summary>Sum of payments received so far.</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>Outstanding balance (GrandTotal - AmountPaid).</summary>
    public decimal Balance => GrandTotal - AmountPaid;

    /// <summary>Place of supply (state) — when it differs from the company state, IGST applies.</summary>
    public string? PlaceOfSupply { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
