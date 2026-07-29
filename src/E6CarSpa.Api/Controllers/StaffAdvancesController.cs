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
public class StaffAdvancesController(AppDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<StaffAdvanceDto>>> List([FromQuery] string? worker)
    {
        var q = db.StaffAdvances.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(worker))
        {
            var w = worker.Trim().ToLower();
            q = q.Where(a => a.WorkerName.ToLower().Contains(w));
        }

        return await q.OrderByDescending(a => a.AdvanceDate).ThenByDescending(a => a.CreatedAt)
            .Take(500)
            .Select(a => new StaffAdvanceDto(a.Id, a.WorkerName, a.Amount, a.AdvanceDate, a.Note))
            .ToListAsync();
    }

    /// <summary>Total advanced per worker, biggest first.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<StaffAdvanceSummaryDto>>> Summary()
    {
        var rows = await db.StaffAdvances.AsNoTracking()
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

    /// <summary>Remove an entry keyed in by mistake.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var advance = await db.StaffAdvances.FindAsync(id);
        if (advance is null) return NotFound();

        db.StaffAdvances.Remove(advance);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
