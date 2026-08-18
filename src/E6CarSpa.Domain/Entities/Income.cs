namespace E6CarSpa.Domain.Entities;

/// <summary>
/// A non-invoice income event (tips, miscellaneous collection, part sale, etc.).
/// Tied to the financial report so every rupee earned is accounted for.
/// </summary>
public class Income : BaseEntity
{
    public string Source { get; set; } = string.Empty;

    /// <summary>Single largest denominator: rupee. Use decimal(12,2) in the DB.</summary>
    public decimal Amount { get; set; }

    /// <summary>The day the cash/non-cash income was received.</summary>
    public DateTime IncomeDate { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }

    public Guid? RecordedByUserId { get; set; }

    // ----- Soft delete -----
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public string? DeletedByUsername { get; set; }
    public bool IsDeleted => DeletedAt is not null;
}
