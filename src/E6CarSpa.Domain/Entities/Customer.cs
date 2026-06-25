namespace E6CarSpa.Domain.Entities;

/// <summary>A customer of the spa. Identified primarily by phone number.</summary>
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>10-digit Indian mobile (stored without country code); used for WhatsApp notifications.</summary>
    public string Phone { get; set; } = string.Empty;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
