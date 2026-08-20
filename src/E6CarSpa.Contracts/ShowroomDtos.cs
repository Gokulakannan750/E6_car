namespace E6CarSpa.Contracts;

// ── Showroom master ──────────────────────────────────────────────

/// <summary>A car showroom the client sends staff to.</summary>
public record ShowroomDto(
 Guid Id, string Name, string Address, string? Phone, string? ContactPerson,
 string? Notes, bool IsActive, DateTime CreatedAt);

public record SaveShowroomRequest(
 string Name, string Address, string? Phone = null,
 string? ContactPerson = null, string? Notes = null);

public record ShowroomPickDto(Guid Id, string Name);

// ── Daily showroom staff assignment ──────────────────────────────

/// <summary>One staff member's daily record at a showroom.</summary>
public record ShowroomDailyStaffDto(
 Guid Id, DateTime AssignmentDate, Guid ShowroomId, string ShowroomName,
 Guid StaffId, string StaffName,
 string AttendanceStatus, int VehiclesAttended, int VehiclesCompleted, decimal AmountGenerated,
 string? Remarks, DateTime CreatedAt);

public record SaveShowroomDailyStaffRequest(
 DateTime AssignmentDate, Guid ShowroomId, Guid StaffId,
 string AttendanceStatus, int VehiclesAttended, int VehiclesCompleted, decimal AmountGenerated,
 string? Remarks);

// ── Performance DTOs ─────────────────────────────────────────────

/// <summary>Summary numbers for a showroom over a date range.</summary>
public record ShowroomPerformanceDto(
 Guid ShowroomId, string ShowroomName,
 int TotalVehiclesAttended, int TotalVehiclesCompleted,
 decimal TotalAmount, int StaffDays,
 decimal AvgVehiclesPerDay, decimal AvgAmountPerDay);

/// <summary>Per-staff breakdown inside a showroom for a date range.</summary>
public record StaffPerformanceDto(
 Guid StaffId, string StaffName,
 int DaysWorked, int TotalVehicles, int TotalCompleted, decimal TotalAmount);

/// <summary>Daily breakdown of a showroom's activity.</summary>
public record DailyShowroomSummaryDto(
 DateTime Date, string StaffName, string AttendanceStatus,
 int VehiclesAttended, int VehiclesCompleted, decimal AmountGenerated);

/// <summary>Aggregate totals for all showrooms (used by the reports page).</summary>
public record ShowroomReportRowDto(
 DateTime AssignmentDate, string ShowroomName, string StaffName,
 string AttendanceStatus,
 int VehiclesAttended, int VehiclesCompleted, decimal AmountGenerated);

public record ShowroomReportSummaryDto(
 int TotalVehiclesAttended, int TotalVehiclesCompleted,
 decimal TotalAmount, int StaffDays);
