using E6CarSpa.Api.Services;
using E6CarSpa.Api.Auth;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;

/// <summary>
/// Salary payments to floor workers. Tied to the Staff table for accountability.
/// </summary>
[RequirePermission(Permission.StaffAdvances)]
public class StaffSalariesController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<StaffSalaryDto>>> List(
        [FromQuery] Guid? staffId, [FromQuery] bool includeDeleted = false)
    {
        var q = db.StaffSalaries.AsNoTracking()
            .Include(s => s.Staff)
            .AsQueryable();
        if (!includeDeleted) q = q.Where(s => s.DeletedAt == null);
        if (staffId.HasValue && staffId.Value != Guid.Empty) q = q.Where(s => s.StaffId == staffId.Value);

        return await q.OrderByDescending(s => s.SalaryDate).ThenByDescending(s => s.CreatedAt)
            .Take(500)
            .Select(s => new StaffSalaryDto(s.Id, s.StaffId, s.Staff.FullName, s.Amount, s.SalaryDate, s.Note,
                s.DeletedAt, s.DeletedByUsername))
            .ToListAsync();
    }

    /// <summary>Total salary paid per worker, biggest first.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<StaffSalarySummaryDto>>> Summary()
    {
        var rows = await db.StaffSalaries.AsNoTracking()
            .Include(s => s.Staff)
            .Where(s => s.DeletedAt == null && s.Staff.IsActive)
            .GroupBy(s => new { s.StaffId, s.Staff.FullName })
            .Select(g => new StaffSalarySummaryDto(g.Key.StaffId, g.Key.FullName,
                g.Sum(x => x.Amount), g.Count()))
            .ToListAsync();

        return rows.OrderByDescending(r => r.TotalPaid).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<StaffSalaryDto>> Create(SaveStaffSalaryRequest req)
    {
        if (req.StaffId == Guid.Empty)
            return BadRequest(new { message = "Select a staff member." });
        if (req.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        var staff = await db.Staff.FindAsync(req.StaffId);
        if (staff is null || !staff.IsActive)
            return BadRequest(new { message = "Selected staff member not found." });

        var salary = new StaffSalary
        {
            StaffId = staff.Id,
            Amount = req.Amount,
            SalaryDate = DateTime.SpecifyKind(req.SalaryDate.Date, DateTimeKind.Utc),
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            RecordedByUserId = CurrentUserId,
            RecordedByUsername = CurrentUsername,
            CreatedAt = DateTime.UtcNow
        };

        db.StaffSalaries.Add(salary);
        await db.SaveChangesAsync();

        var dto = new StaffSalaryDto(salary.Id, salary.StaffId, staff.FullName, salary.Amount,
            salary.SalaryDate, salary.Note, salary.DeletedAt, salary.DeletedByUsername);
        await audit.LogAsync("StaffSalary.Create",
            $"staff={staff.FullName}; amount={salary.Amount:0.##}; date={salary.SalaryDate:yyyy-MM-dd}");
        return dto;
    }

    /// <summary>Soft-delete: marks the entry obsolete. Historical totals exclude it.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var salary = await db.StaffSalaries.FindAsync(id);
        if (salary is null) return NotFound();
        if (salary.DeletedAt is not null) return NoContent();

        salary.DeletedAt = DateTime.UtcNow;
        salary.DeletedByUserId = CurrentUserId;
        salary.DeletedByUsername = CurrentUsername;
        salary.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await audit.LogAsync("StaffSalary.Delete",
            $"staffId={salary.StaffId}; amount={salary.Amount:0.##}");

        return NoContent();
    }
}
