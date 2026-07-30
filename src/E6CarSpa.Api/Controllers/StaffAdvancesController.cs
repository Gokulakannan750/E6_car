using E6CarSpa.Api.Services;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;

/// <summary>
/// Record of cash advances given to workers. Requires a signed-in user (wage data), like every
/// endpoint except login — see the fallback policy in Program.cs.
/// </summary>
public class StaffAdvancesController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    /// <param name="includeDeleted">
    /// Also return entries marked obsolete. Off by default, so ordinary use shows only live
    /// advances; the clients switch it on to display the audit trail.
    /// </param>
    [HttpGet]
    public async Task<ActionResult<List<StaffAdvanceDto>>> List(
        [FromQuery] string? worker, [FromQuery] bool includeDeleted = false)
    {
        var q = db.StaffAdvances.AsNoTracking().AsQueryable();
        if (!includeDeleted) q = q.Where(a => a.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(worker))
        {
            var w = worker.Trim().ToLower();
            q = q.Where(a => a.WorkerName.ToLower().Contains(w));
        }

        return await q.OrderByDescending(a => a.AdvanceDate).ThenByDescending(a => a.CreatedAt)
            .Take(500)
            .Select(a => new StaffAdvanceDto(a.Id, a.WorkerName, a.Amount, a.AdvanceDate, a.Note,
                a.DeletedAt, a.DeletedByUsername))
            .ToListAsync();
    }

    /// <summary>Total advanced per worker, biggest first. Obsolete entries never count.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<StaffAdvanceSummaryDto>>> Summary()
    {
        var rows = await db.StaffAdvances.AsNoTracking()
            .Where(a => a.DeletedAt == null)
            .GroupBy(a => a.WorkerName)
            .Select(g => new StaffAdvanceSummaryDto(g.Key, g.Sum(x => x.Amount), g.Count()))
            .ToListAsync();

        return rows.OrderByDescending(r => r.TotalAdvanced).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<StaffAdvanceDto>> Create(SaveStaffAdvanceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.WorkerName))
            return BadRequest(new { message = "Enter the worker's name." });
        if (req.Amount <= 0)
            return BadRequest(new { message = "Advance amount must be greater than zero." });

        var advance = new StaffAdvance
        {
            WorkerName = req.WorkerName.Trim(),
            Amount = req.Amount,
            // Store the chosen day as UTC midnight — Npgsql requires UTC for timestamptz.
            AdvanceDate = DateTime.SpecifyKind(req.AdvanceDate.Date, DateTimeKind.Utc),
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            RecordedByUserId = CurrentUserId
        };

        db.StaffAdvances.Add(advance);
        await db.SaveChangesAsync();

        return new StaffAdvanceDto(advance.Id, advance.WorkerName, advance.Amount, advance.AdvanceDate, advance.Note);
    }

    /// <summary>
    /// Mark an entry keyed in by mistake as obsolete. The row is NOT erased — this is money data,
    /// so it stays for the audit trail stamped with who removed it and when, and simply stops
    /// counting towards the totals.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var advance = await db.StaffAdvances.FindAsync(id);
        if (advance is null) return NotFound();
        // Deleting twice must not overwrite who did it first.
        if (advance.DeletedAt is not null) return NoContent();

        advance.DeletedAt = DateTime.UtcNow;
        advance.DeletedByUserId = CurrentUserId;
        advance.DeletedByUsername = CurrentUsername;
        advance.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await audit.LogAsync("StaffAdvance.Delete",
            $"worker={advance.WorkerName}; amount={advance.Amount:0.##}; date={advance.AdvanceDate:yyyy-MM-dd}");

        return NoContent();
    }
}
