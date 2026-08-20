using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E6CarSpa.Domain.Entities;

/// <summary>
/// One row per (date, staff, showroom). Captures attendance, timings, and the day's
/// vehicle / amount totals for that staff member at that showroom.
/// </summary>
public sealed class ShowroomDailyStaff
{
 [Key]
 public Guid Id { get; set; }

 /// <summary>Assignment date. Stored as DateTime in PostgreSQL.</summary>
 public DateTime AssignmentDate { get; set; }

 /// <summary>Nullable so the row can be saved before the showroom is finalised.</summary>
 public Guid? ShowroomId { get; set; }

 /// <summary>Staff assigned for the day. References the EXISTING Staff master.</summary>
 public Guid StaffId { get; set; }

 /// <summary>Present / Absent / HalfDay / Leave.</summary>
 [MaxLength(40)]
 public string AttendanceStatus { get; set; } = AttendanceStatuses.Present;

 /// <summary>Vehicles taken up for work this day.</summary>
 public int VehiclesAttended { get; set; }

 /// <summary>Vehicles completed this day. Must be &lt;= VehiclesAttended.</summary>
 public int VehiclesCompleted { get; set; }

 /// <summary>Amount generated for the showroom this day (in INR).</summary>
 [Column(TypeName = "decimal(14,2)")]
 public decimal AmountGenerated { get; set; }

 [MaxLength(500)]
 public string? Remarks { get; set; }

 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

 // ---- Navigation ----

 [ForeignKey(nameof(ShowroomId))]
 public Showroom? Showroom { get; set; }

 [ForeignKey(nameof(StaffId))]
 public Staff? Staff { get; set; }
}

/// <summary>
/// Constants for the AttendanceStatus column so the whole app uses the same vocabulary.
/// </summary>
public static class AttendanceStatuses
{
 public const string Present = "Present";
 public const string Absent = "Absent";
 public const string HalfDay = "HalfDay";
 public const string Leave = "Leave";

 public static readonly string[] All = [Present, Absent, HalfDay, Leave];
}
