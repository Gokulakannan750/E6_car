using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Domain.Entities;

/// <summary>
/// An immutable ledger entry for every change to a product's stock level.
/// Stock on hand is the sum of all movement quantities (purchases positive, consumption negative).
/// </summary>
public class StockMovement : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>Signed change in stock: positive for purchase, negative for consumption / return.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Cost per unit at the time of movement (for purchases / valuation).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Free-text reference: supplier invoice no., stock-take note, etc.</summary>
    public string? Reference { get; set; }

    /// <summary>When consumption is tied to a job, links back to the invoice that consumed it.</summary>
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public string? Note { get; set; }
}
