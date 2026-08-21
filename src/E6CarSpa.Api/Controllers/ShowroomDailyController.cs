using E6CarSpa.Api.Auth;
using E6CarSpa.Api.Services;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;

/// <summary>
/// Daily showroom staff assignments and showroom performance data.
/// </summary>
[ApiController]
[Route("api/showroom-daily")]
public class ShowroomDailyController(AppDbContext db, AuditService audit) : ApiControllerBase
{
 // ──────────────────────── Daily assignment CRUD ────────────────────────

 /// <summary>
 /// All assignments in a date range (or all if neither query param is given).
 /// Sorted newest first, capped at 500 rows.
 /// </summary>
 [HttpGet]
 public async Task<ActionResult<List<ShowroomDailyStaffDto>>> List(
 [FromQuery] DateTime? from, [FromQuery] DateTime? to,
 [FromQuery] Guid? showroomId, [FromQuery] Guid? staffId,
 [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
 {
 try
 {
 var q = db.ShowroomDailyStaff.AsNoTracking()
 .Include(d => d.Showroom)
 .Include(d => d.Staff)
 .AsQueryable();

 if (from.HasValue) q = q.Where(d => d.AssignmentDate >= from.Value.Date);
 if (to.HasValue) q = q.Where(d => d.AssignmentDate < to.Value.Date.AddDays(1));
 if (showroomId.HasValue) q = q.Where(d => d.ShowroomId == showroomId.Value);
 if (staffId.HasValue) q = q.Where(d => d.StaffId == staffId.Value);

 pageSize = Math.Clamp(pageSize, 1, 500);
 page = Math.Max(page, 1);

 var total = await q.CountAsync();
 var items = await q.OrderByDescending(d => d.AssignmentDate).ThenByDescending(d => d.CreatedAt)
 .Skip((page - 1) * pageSize)
 .Take(pageSize)
 .Select(d => new ShowroomDailyStaffDto(
 d.Id, d.AssignmentDate, d.ShowroomId!.Value, d.Showroom.Name,
 d.StaffId, d.Staff.FullName,
 d.AttendanceStatus,
 d.VehiclesAttended, d.VehiclesCompleted, d.AmountGenerated,
 d.Remarks, d.CreatedAt))
 .ToListAsync();

 Response.Headers.Append("X-Total-Count", total.ToString());
 return items;
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not load daily assignments",
 detail: $"{ex.GetType().Name}: {ex.Message}",
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 /// <summary>Assignments for a single date.</summary>
 [HttpGet("by-date")]
 public async Task<ActionResult<List<ShowroomDailyStaffDto>>> ByDate([FromQuery] DateTime date)
 {
 try
 {
 var start = date.Date;
 var end = start.AddDays(1);
 var rows = await db.ShowroomDailyStaff.AsNoTracking()
 .Include(d => d.Showroom)
 .Include(d => d.Staff)
 .Where(d => d.AssignmentDate >= start && d.AssignmentDate < end)
 .OrderBy(d => d.Showroom.Name).ThenBy(d => d.Staff.FullName)
 .Select(d => new ShowroomDailyStaffDto(
 d.Id, d.AssignmentDate, d.ShowroomId!.Value, d.Showroom.Name,
 d.StaffId, d.Staff.FullName,
 d.AttendanceStatus,
 d.VehiclesAttended, d.VehiclesCompleted, d.AmountGenerated,
 d.Remarks, d.CreatedAt))
 .ToListAsync();

 return Ok(rows);
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not load assignments for date",
 detail: $"{ex.GetType().Name}: {ex.Message}",
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 [HttpPost]
 public async Task<ActionResult<ShowroomDailyStaffDto>> Create(SaveShowroomDailyStaffRequest req)
 {
 // ── validation ──
 if (req.VehiclesAttended < 0)
 return BadRequest(new { message = "Vehicles attended cannot be negative." });
 if (req.VehiclesCompleted < 0)
 return BadRequest(new { message = "Vehicles completed cannot be negative." });
 if (req.VehiclesCompleted > req.VehiclesAttended)
 return BadRequest(new { message = "Completed vehicles cannot exceed attended vehicles." });
 if (req.AmountGenerated < 0)
 return BadRequest(new { message = "Amount cannot be negative." });


 try
 {
 // Verify showroom and staff exist and are active.
 var showroom = await db.Showrooms.FindAsync(req.ShowroomId);
 if (showroom is null || !showroom.IsActive)
 return BadRequest(new { message = "Selected showroom not found or inactive." });

 var staff = await db.Staff.FindAsync(req.StaffId);
 if (staff is null || !staff.IsActive)
 return BadRequest(new { message = "Selected staff member not found or inactive." });

 var assignment = new ShowroomDailyStaff
 {
 AssignmentDate = req.AssignmentDate.Date,
 ShowroomId = req.ShowroomId,
 StaffId = req.StaffId,
 AttendanceStatus = string.IsNullOrWhiteSpace(req.AttendanceStatus) ? AttendanceStatuses.Present : req.AttendanceStatus,

 VehiclesAttended = req.VehiclesAttended,
 VehiclesCompleted = req.VehiclesCompleted,
 AmountGenerated = req.AmountGenerated,
 Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim()
 };

 db.ShowroomDailyStaff.Add(assignment);
 await db.SaveChangesAsync();

 // Return the joined DTO.
 var dto = await db.ShowroomDailyStaff.AsNoTracking()
 .Include(d => d.Showroom)
 .Include(d => d.Staff)
 .Where(d => d.Id == assignment.Id)
 .Select(d => new ShowroomDailyStaffDto(
 d.Id, d.AssignmentDate, d.ShowroomId!.Value, d.Showroom.Name,
 d.StaffId, d.Staff.FullName,
 d.AttendanceStatus,
 d.VehiclesAttended, d.VehiclesCompleted, d.AmountGenerated,
 d.Remarks, d.CreatedAt))
 .FirstAsync();

 await audit.LogAsync("ShowroomDailyStaff.Create",
 $"{dto.StaffName} @ {dto.ShowroomName} on {dto.AssignmentDate:yyyy-MM-dd}");

 return CreatedAtAction(nameof(List), new { id = dto.Id }, dto);
 }
 catch (DbUpdateException)
 {
 // Unique constraint (date + staff_id) violation.
 return Conflict(new { message = "This staff member is already assigned for this date." });
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not create assignment",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 [HttpPut("{id:guid}")]
 public async Task<ActionResult<ShowroomDailyStaffDto>> Update(Guid id, SaveShowroomDailyStaffRequest req)
 {
 if (req.VehiclesAttended < 0)
 return BadRequest(new { message = "Vehicles attended cannot be negative." });
 if (req.VehiclesCompleted < 0)
 return BadRequest(new { message = "Vehicles completed cannot be negative." });
 if (req.VehiclesCompleted > req.VehiclesAttended)
 return BadRequest(new { message = "Completed vehicles cannot exceed attended vehicles." });
 if (req.AmountGenerated < 0)
 return BadRequest(new { message = "Amount cannot be negative." });


 try
 {
 var assignment = await db.ShowroomDailyStaff.FindAsync(id);
 if (assignment is null) return NotFound();

 // Check showroom + staff still valid.
 var showroom = await db.Showrooms.FindAsync(req.ShowroomId);
 if (showroom is null) return BadRequest(new { message = "Selected showroom not found." });
 var staff = await db.Staff.FindAsync(req.StaffId);
 if (staff is null) return BadRequest(new { message = "Selected staff member not found." });

 assignment.AssignmentDate = req.AssignmentDate.Date;
 assignment.ShowroomId = req.ShowroomId;
 assignment.StaffId = req.StaffId;
 assignment.AttendanceStatus = string.IsNullOrWhiteSpace(req.AttendanceStatus) ? AttendanceStatuses.Present : req.AttendanceStatus;

 assignment.VehiclesAttended = req.VehiclesAttended;
 assignment.VehiclesCompleted = req.VehiclesCompleted;
 assignment.AmountGenerated = req.AmountGenerated;
 assignment.Remarks = string.IsNullOrWhiteSpace(req.Remarks) ? null : req.Remarks.Trim();
 assignment.UpdatedAt = DateTime.UtcNow;

 await db.SaveChangesAsync();

 var dto = await db.ShowroomDailyStaff.AsNoTracking()
 .Include(d => d.Showroom)
 .Include(d => d.Staff)
 .Where(d => d.Id == id)
 .Select(d => new ShowroomDailyStaffDto(
 d.Id, d.AssignmentDate, d.ShowroomId!.Value, d.Showroom.Name,
 d.StaffId, d.Staff.FullName,
 d.AttendanceStatus,
 d.VehiclesAttended, d.VehiclesCompleted, d.AmountGenerated,
 d.Remarks, d.CreatedAt))
 .FirstAsync();

 await audit.LogAsync("ShowroomDailyStaff.Update",
 $"{dto.StaffName} @ {dto.ShowroomName} on {dto.AssignmentDate:yyyy-MM-dd}");
 return Ok(dto);
 }
 catch (DbUpdateException)
 {
 return Conflict(new { message = "This staff member is already assigned for this date." });
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not update assignment",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 [HttpDelete("{id:guid}")]
 public async Task<IActionResult> Delete(Guid id)
 {
 try
 {
 var assignment = await db.ShowroomDailyStaff.FindAsync(id);
 if (assignment is null) return NotFound();

 db.ShowroomDailyStaff.Remove(assignment);
 await db.SaveChangesAsync();

 await audit.LogAsync("ShowroomDailyStaff.Delete",
 $"assignment {id} removed");
 return NoContent();
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not delete assignment",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 // ──────────────────────── Performance ────────────────────────

 /// <summary>Summary for a showroom over a date range.</summary>
 [HttpGet("performance/{showroomId:guid}")]
 public async Task<ActionResult<ShowroomPerformanceDto>> Performance(
 Guid showroomId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
 {
 try
 {
 var start = from?.Date ?? DateTime.MinValue;
 var end = to?.Date.AddDays(1) ?? DateTime.MaxValue;

 var q = db.ShowroomDailyStaff.AsNoTracking()
 .Where(d => d.ShowroomId == showroomId && d.AssignmentDate >= start && d.AssignmentDate < end);

 var totalAttended = await q.SumAsync(d => d.VehiclesAttended);
 var totalCompleted = await q.SumAsync(d => d.VehiclesCompleted);
 var totalAmount = await q.SumAsync(d => d.AmountGenerated);
 var staffDays = await q.CountAsync();

 var distinctDays = await q.Select(d => d.AssignmentDate.Date).Distinct().CountAsync();
 var avgVehicles = distinctDays > 0 ? totalAttended / (decimal)distinctDays : 0;
 var avgAmount = distinctDays > 0 ? totalAmount / (decimal)distinctDays : 0;

 var showroom = await db.Showrooms.AsNoTracking()
 .Where(s => s.Id == showroomId)
 .Select(s => s.Name)
 .FirstOrDefaultAsync();

 if (showroom is null) return NotFound();

 return Ok(new ShowroomPerformanceDto(
 showroomId, showroom,
 totalAttended, totalCompleted, totalAmount, staffDays,
 Math.Round(avgVehicles, 1), Math.Round(avgAmount, 1)));
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not load performance data",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 /// <summary>Per-staff performance breakdown inside a showroom for a date range.</summary>
 [HttpGet("performance/{showroomId:guid}/staff")]
 public async Task<ActionResult<List<StaffPerformanceDto>>> PerformanceByStaff(
 Guid showroomId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
 {
 try
 {
 var start = from?.Date ?? DateTime.MinValue;
 var end = to?.Date.AddDays(1) ?? DateTime.MaxValue;

 var rawRows = await db.ShowroomDailyStaff.AsNoTracking()
 .Include(d => d.Staff)
 .Where(d => d.ShowroomId == showroomId && d.AssignmentDate >= start && d.AssignmentDate < end)
 .ToListAsync();

 var rows = rawRows
 .GroupBy(d => new { d.StaffId, d.Staff.FullName })
 .Select(g => new StaffPerformanceDto(
 g.Key.StaffId, g.Key.FullName,
 g.Select(d => d.AssignmentDate.Date).Distinct().Count(),
 g.Sum(d => d.VehiclesAttended),
 g.Sum(d => d.VehiclesCompleted),
 g.Sum(d => d.AmountGenerated)))
 .OrderByDescending(r => r.TotalAmount)
 .ToList();

 return Ok(rows);
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not load staff performance",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 /// <summary>Daily breakdown for a showroom in a date range.</summary>
 [HttpGet("performance/{showroomId:guid}/daily")]
 public async Task<ActionResult<List<DailyShowroomSummaryDto>>> DailyBreakdown(
 Guid showroomId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
 {
 try
 {
 var start = from?.Date ?? DateTime.MinValue;
 var end = to?.Date.AddDays(1) ?? DateTime.MaxValue;

 var rows = await db.ShowroomDailyStaff.AsNoTracking()
 .Include(d => d.Staff)
 .Where(d => d.ShowroomId == showroomId && d.AssignmentDate >= start && d.AssignmentDate < end)
 .OrderBy(d => d.AssignmentDate).ThenBy(d => d.Staff.FullName)
 .Select(d => new DailyShowroomSummaryDto(
 d.AssignmentDate, d.Staff.FullName, d.AttendanceStatus,
 d.VehiclesAttended, d.VehiclesCompleted, d.AmountGenerated))
 .ToListAsync();

 return Ok(rows);
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not load daily breakdown",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 // ──────────────────────── Reports ────────────────────────

 /// <summary>Detailed report rows with optional filters.</summary>
 [HttpGet("report")]
 public async Task<ActionResult<List<ShowroomReportRowDto>>> Report(
 [FromQuery] DateTime? from, [FromQuery] DateTime? to,
 [FromQuery] Guid? showroomId, [FromQuery] Guid? staffId,
 [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
 {
 try
 {
 var start = from?.Date ?? DateTime.MinValue;
 var end = to?.Date.AddDays(1) ?? DateTime.MaxValue;

 var q = db.ShowroomDailyStaff.AsNoTracking()
 .Include(d => d.Showroom)
 .Include(d => d.Staff)
 .Where(d => d.AssignmentDate >= start && d.AssignmentDate < end);

 if (showroomId.HasValue) q = q.Where(d => d.ShowroomId == showroomId.Value);
 if (staffId.HasValue) q = q.Where(d => d.StaffId == staffId.Value);

 var rows = await q.OrderByDescending(d => d.AssignmentDate)
 .Select(d => new ShowroomReportRowDto(
 d.AssignmentDate, d.Showroom.Name, d.Staff.FullName,
 d.AttendanceStatus,
 d.VehiclesAttended, d.VehiclesCompleted, d.AmountGenerated))
 .ToListAsync();

 return Ok(rows);
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not load report",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 /// <summary>Summary totals for the report.</summary>
 [HttpGet("report/summary")]
 public async Task<ActionResult<ShowroomReportSummaryDto>> ReportSummary(
 [FromQuery] DateTime? from, [FromQuery] DateTime? to,
 [FromQuery] Guid? showroomId, [FromQuery] Guid? staffId,
 [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
 {
 try
 {
 var start = from?.Date ?? DateTime.MinValue;
 var end = to?.Date.AddDays(1) ?? DateTime.MaxValue;

 var q = db.ShowroomDailyStaff.AsNoTracking()
 .Where(d => d.AssignmentDate >= start && d.AssignmentDate < end);

 if (showroomId.HasValue) q = q.Where(d => d.ShowroomId == showroomId.Value);
 if (staffId.HasValue) q = q.Where(d => d.StaffId == staffId.Value);

 var totalVehiclesAttended = await q.SumAsync(d => d.VehiclesAttended);
 var totalVehiclesCompleted = await q.SumAsync(d => d.VehiclesCompleted);
 var totalAmount = await q.SumAsync(d => d.AmountGenerated);
 var staffDays = await q.CountAsync();

 return Ok(new ShowroomReportSummaryDto(
 totalVehiclesAttended, totalVehiclesCompleted, totalAmount, staffDays));
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not load report summary",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }
}
