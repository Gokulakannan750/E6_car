using System.ComponentModel.DataAnnotations;

namespace E6CarSpa.Domain.Entities;

/// <summary>
/// A partner / outlet showroom where the E6 Car Spa team visits to do detailing work.
/// The company sends a technical team to each location on demand; this entity is the
/// location master (who), and the visits themselves are tracked as <see cref="ShowroomVisit"/>.
/// </summary>
public class Showroom : BaseEntity
{
    /// <summary>Display name of the showroom / partner location.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Physical address of the showroom.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Contact phone number. Optional — some showrooms don't share one.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Whether the showroom is still active. Inactive showrooms are hidden from pick lists but
    /// their visit history is preserved so past takings still read correctly.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<ShowroomVisit> Visits { get; set; } = new List<ShowroomVisit>();
}