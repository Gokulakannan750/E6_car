using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E6CarSpa.Domain.Entities;

/// <summary>
/// Showroom master — a car showroom / dealership where staff are sent to perform
/// detailing / valeting work on customer vehicles.
/// </summary>
public sealed class Showroom
{
 [Key]
 public Guid Id { get; set; }

 [Required, MaxLength(160)]
 public string Name { get; set; } = string.Empty;

 [MaxLength(500)]
 public string? Address { get; set; }

 [MaxLength(40)]
 public string? Phone { get; set; }

 [MaxLength(120)]
 public string? ContactPerson { get; set; }

 [MaxLength(500)]
 public string? Notes { get; set; }

 public bool IsActive { get; set; } = true;

 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

 // ---- Navigation ----

 [InverseProperty(nameof(ShowroomDailyStaff.Showroom))]
 public List<ShowroomDailyStaff> DailyAssignments { get; set; } = [];
}
