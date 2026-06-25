namespace E6CarSpa.Domain.Entities;

/// <summary>
/// Bill-of-materials line: how much of a given product a service typically consumes.
/// Used to auto-deduct inventory when a job is completed.
/// </summary>
public class ServiceProduct : BaseEntity
{
    public Guid ServiceId { get; set; }
    public Service? Service { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Default quantity of the product consumed per unit of the service.</summary>
    public decimal DefaultQuantity { get; set; }
}
