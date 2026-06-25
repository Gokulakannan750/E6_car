namespace E6CarSpa.Domain.Entities;

/// <summary>A vehicle belonging to a customer. Tracked only by its registration number.</summary>
public class Vehicle : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Registration / number plate, e.g. "TN33 AB 1234". Stored normalised (upper-case, no spaces) for lookup.</summary>
    public string CarNumber { get; set; } = string.Empty;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
