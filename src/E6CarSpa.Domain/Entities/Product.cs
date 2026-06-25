namespace E6CarSpa.Domain.Entities;

/// <summary>
/// An inventory item, tracked as a simple count of units: bottles of polish/coating/shampoo,
/// microfiber cloths, grinding discs, brushes, pads, etc.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Grouping, e.g. "Coating", "Polish", "Consumable", "Bodyshop".</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Number of units currently in stock (bottles / pieces).</summary>
    public decimal StockQuantity { get; set; }

    /// <summary>Low-stock threshold that flags the product for reordering.</summary>
    public decimal ReorderLevel { get; set; }

    /// <summary>Latest purchase cost per unit (for valuation and consumption costing).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>HSN code (goods) for when a product is sold directly on a bill.</summary>
    public string HsnSac { get; set; } = string.Empty;

    public decimal GstRate { get; set; } = 18m;

    public bool IsActive { get; set; } = true;

    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}
