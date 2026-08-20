using E6CarSpa.Api.Services;
using E6CarSpa.Api.Auth;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;

/// <summary>Cash-advance records for floor workers. Staff master lives in <see cref="StaffController"/>.</summary>
public class StaffAdvancesController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    /// <param name="includeDeleted">Also return entries marked obsolete.</param>
    [HttpGet]
    public async Task<ActionResult<List<StaffAdvanceDto>>> List(
        [FromQuery] string? worker, [FromQuery] bool includeDeleted = false)
    {
        try
        {
            var q = db.StaffAdvances.AsNoTracking()
                .Include(a => a.Staff)
                .AsQueryable();
            if (!includeDeleted) q = q.Where(a => a.DeletedAt == null);
            if (!string.IsNullOrWhiteSpace(worker))
            {
                var w = worker.Trim().ToLower();
                q = q.Where(a => a.Staff.FullName.ToLower().Contains(w));
            }

            return await q.OrderByDescending(a => a.AdvanceDate).ThenByDescending(a => a.CreatedAt)
                .Take(500)
                .Select(a => new StaffAdvanceDto(a.Id, a.StaffId, a.Staff.FullName, a.Amount, a.AdvanceDate, a.Note,
                    a.DeletedAt, a.DeletedByUsername))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not load advances",
                detail: ex.InnerException != null ? ex.InnerException.Message : ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>Total advanced per worker, biggest first. Obsolete entries never count.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<StaffAdvanceSummaryDto>>> Summary()
    {
        try
        {
            var rows = await db.StaffAdvances.AsNoTracking()
                .Where(a => a.DeletedAt == null)
                .GroupBy(a => new { a.StaffId, a.Staff.FullName })
                .Select(g => new StaffAdvanceSummaryDto(g.Key.StaffId, g.Key.FullName,
                    g.Sum(x => x.Amount), g.Count()))
                .ToListAsync();

            return rows.OrderByDescending(r => r.TotalAdvanced).ToList();
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not load summary",
                detail: ex.InnerException != null ? ex.InnerException.Message : ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost]
    public async Task<ActionResult<StaffAdvanceDto>> Create(SaveStaffAdvanceRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { message = "Advance amount must be greater than zero." });

        try
        {
            var staff = await db.Staff.FindAsync(req.StaffId);
            if (staff is null || !staff.IsActive)
                return BadRequest(new { message = "Selected staff member not found." });

            var advance = new StaffAdvance
            {
                StaffId = staff.Id,
                Amount = req.Amount,
                AdvanceDate = DateTime.SpecifyKind(req.AdvanceDate.Date, DateTimeKind.Utc),
                Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
                RecordedByUserId = CurrentUserId,
                RecordedByUsername = CurrentUsername,
                CreatedAt = DateTime.UtcNow
            };

            db.StaffAdvances.Add(advance);
            await db.SaveChangesAsync();

            return new StaffAdvanceDto(advance.Id, advance.StaffId, staff.FullName,
                advance.Amount, advance.AdvanceDate, advance.Note,
                advance.DeletedAt, advance.DeletedByUsername);
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not record advance",
                detail: ex.InnerException != null ? ex.InnerException.Message : ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Mark an entry keyed in by mistake as obsolete. The row is NOT erased — this is money data,
    /// so it stays for the audit trail stamped with who removed it and when, and simply stops
    /// counting towards the totals.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var advance = await db.StaffAdvances.FindAsync(id);
            if (advance is null) return NotFound();
            if (advance.DeletedAt is not null) return NoContent();

            advance.DeletedAt = DateTime.UtcNow;
            advance.DeletedByUserId = CurrentUserId;
            advance.DeletedByUsername = CurrentUsername;
            advance.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            await audit.LogAsync("StaffAdvance.Delete",
                $"amount={advance.Amount:0.##}; date={advance.AdvanceDate:yyyy-MM-dd}");

            return NoContent();
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not delete advance",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
