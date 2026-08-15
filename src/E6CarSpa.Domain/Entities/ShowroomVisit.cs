namespace E6CarSpa.Domain.Entities;

/// <summary>
/// One visit (or working day) at a showroom: who from the team went, how many cars they attended,
/// how much money came in. Multiple visits per showroom are expected (one per day they're called
/// in), so the per-showroom total is the SUM of all visits' amounts.
/// </summary>
public class ShowroomVisit : BaseEntity
{
    public Guid ShowroomId { get; set; }
    public Showroom? Showroom { get; set; }

    /// <summary>The day of the visit. Stored as UTC midnight so dates line up cleanly in reports.</summary>
    public DateTime VisitDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Free-text description of who went from the technical team — e.g. "2 detailers + 1 helper".
    /// Worker names aren't app users (same as Staff Advances), so this is typed in, not a dropdown.
    /// </summary>
    public string TeamSent { get; set; } = string.Empty;

    /// <summary>How many vehicles the team worked on during this visit.</summary>
    public int VehiclesAttended { get; set; }

    /// <summary>Total revenue collected from this visit, in rupees.</summary>
    public decimal Amount { get; set; }

    /// <summary>Optional free-text notes for the visit.</summary>
    public string? Note { get; set; }
}