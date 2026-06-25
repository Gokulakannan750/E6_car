namespace E6CarSpa.Domain.Entities;

/// <summary>
/// A single line on an invoice — usually a service, occasionally a product sold directly.
/// GST is captured per line because rates/HSN can differ between items.
/// </summary>
public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>Set when the line is a catalogue service.</summary>
    public Guid? ServiceId { get; set; }
    public Service? Service { get; set; }

    /// <summary>Set when the line is a product sold over the counter.</summary>
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Snapshot of the name at billing time (so later catalogue edits don't change old invoices).</summary>
    public string Description { get; set; } = string.Empty;

    public string HsnSac { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }

    public decimal GstRate { get; set; }

    /// <summary>Line amount before tax: (Quantity * UnitPrice) - DiscountAmount.</summary>
    public decimal TaxableValue { get; set; }

    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }

    /// <summary>Line total including tax.</summary>
    public decimal LineTotal { get; set; }
}
