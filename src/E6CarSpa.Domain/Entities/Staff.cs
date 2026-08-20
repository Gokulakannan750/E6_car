using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace E6CarSpa.Domain.Entities;

/// <summary>A floor worker whose name appears on cash-advance records. Staff are NOT app users -- they
/// don't log in. This table exists so names are centralised: the same person referenced from
/// multiple advances, future features, or reports always resolves to a single record.
/// </summary>
public class Staff
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Full name as shown everywhere (invoices, advances, reports).</summary>
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Inactive staff are hidden from pickers but their historical advances remain visible.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ----- Navigation -----
    public List<StaffAdvance> Advances { get; set; } = new();
    public List<StaffSalary> Salaries { get; set; } = new();
 public List<ShowroomDailyStaff> ShowroomAssignments { get; set; } = new();
}
