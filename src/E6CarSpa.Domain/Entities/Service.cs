namespace E6CarSpa.Domain.Entities;

/// <summary>
/// A service offered by the spa (Ceramic Coating, Teflon Polishing, Water Wash, etc.).
/// These populate the "select services" screen during billing.
/// </summary>
public class Service : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Grouping shown in the UI, e.g. "Coating", "Wash", "Polishing", "Bodyshop".</summary>
    public string Category { get; set; } = string.Empty;

    public decimal DefaultPrice { get; set; }

    /// <summary>SAC (Services Accounting Code) for GST on services.</summary>
    public string HsnSac { get; set; } = "999719";

    /// <summary>GST percentage applied to this service (e.g. 18).</summary>
    public decimal GstRate { get; set; } = 18m;

    public bool IsActive { get; set; } = true;

    /// <summary>Products this service typically consumes (bill of materials) for auto stock deduction.</summary>
    public ICollection<ServiceProduct> BillOfMaterials { get; set; } = new List<ServiceProduct>();
}
