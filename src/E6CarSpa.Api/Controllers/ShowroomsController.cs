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

/// <summary>Partner showroom locations and the visits made to each.</summary>
[RequirePermission(Permission.Showroom)]
public class ShowroomsController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    // ═══════════════════════════════════════
    //  Showrooms (master records)
    // ═══════════════════════════════════════

    /// <param name="includeInactive">
    /// Also return showrooms that were marked inactive. Off by default so the pick lists
    /// and the table only show active locations; the client toggles this for audit/rollback.
    /// </param>
    [HttpGet]
    public async Task<ActionResult<List<ShowroomDto>>> List([FromQuery] bool includeInactive = false)
    {
        var q = db.Showrooms.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(s => s.IsActive);

        return await q.OrderBy(s => s.Name)
            .Select(s => new ShowroomDto(s.Id, s.Name, s.Address, s.Phone, s.IsActive))
            .ToListAsync();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShowroomDto>> Get(Guid id)
    {
        var s = await db.Showrooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return s is null ? NotFound() : new ShowroomDto(s.Id, s.Name, s.Address, s.Phone, s.IsActive);
    }

    [HttpPost]
    public async Task<ActionResult<ShowroomDto>> Create(SaveShowroomRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Enter the showroom name." });
        if (string.IsNullOrWhiteSpace(req.Address))
            return BadRequest(new { message = "Enter the showroom address." });

        // Check duplicate name (unique index would throw 500 on conflict — this gives a clean message)
        var nameTaken = await db.Showrooms.AnyAsync(s => s.Name.ToLower() == req.Name.Trim().ToLower());
        if (nameTaken) return Conflict(new { message = $"A showroom named '{req.Name.Trim()}' already exists." });

        var showroom = new Showroom
        {
            Name = req.Name.Trim(),
            Address = req.Address.Trim(),
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim()
        };

        db.Showrooms.Add(showroom);
        await db.SaveChangesAsync();

        await audit.LogAsync("Showroom.Create", $"name={showroom.Name}");

        return CreatedAtAction(nameof(Get), new { id = showroom.Id },
            new ShowroomDto(showroom.Id, showroom.Name, showroom.Address, showroom.Phone, showroom.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveShowroomRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Enter the showroom name." });
        if (string.IsNullOrWhiteSpace(req.Address))
            return BadRequest(new { message = "Enter the showroom address." });

        var showroom = await db.Showrooms.FindAsync(id);
        if (showroom is null) return NotFound();

        var nameTaken = await db.Showrooms
            .AnyAsync(s => s.Id != id && s.Name.ToLower() == req.Name.Trim().ToLower());
        if (nameTaken) return Conflict(new { message = $"A showroom named '{req.Name.Trim()}' already exists." });

        showroom.Name = req.Name.Trim();
        showroom.Address = req.Address.Trim();
        showroom.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        showroom.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await audit.LogAsync("Showroom.Update", $"name={showroom.Name}");

        return NoContent();
    }

    /// <summary>Mark a showroom as inactive rather than delete it — visit history stays readable.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var showroom = await db.Showrooms.FindAsync(id);
        if (showroom is null) return NotFound();

        showroom.IsActive = false;
        showroom.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.LogAsync("Showroom.Delete", $"name={showroom.Name}");

        return NoContent();
    }

    // ═══════════════════════════════════════
    //  Visits
    // ═══════════════════════════════════════

    /// <param name="from">Filter visits on or after this date (UTC). Pass as yyyy-MM-dd.</param>
    /// <param name="to">Filter visits on or before this date (UTC). Pass as yyyy-MM-dd.</param>
    [HttpGet("{id:guid}/visits")]
    public async Task<ActionResult<List<ShowroomVisitDto>>> ListVisits(
        Guid id, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var showroom = await db.Showrooms.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (showroom is null) return NotFound();

        var q = db.ShowroomVisits.AsNoTracking().Where(v => v.ShowroomId == id);

        if (from is DateTime f) q = q.Where(v => v.VisitDate >= f);
        if (to is DateTime t) q = q.Where(v => v.VisitDate <= t);

        return await q.OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.CreatedAt)
            .Select(v => new ShowroomVisitDto(v.Id, v.ShowroomId, showroom.Name,
                v.VisitDate, v.TeamSent, v.VehiclesAttended, v.Amount, v.Note))
            .ToListAsync();
    }

    /// <summary>Total across all showrooms (optionally filtered by date range).</summary>
    [HttpGet("visits/summary")]
    public async Task<ActionResult<List<ShowroomVisitSummaryDto>>> Summary(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var q = db.ShowroomVisits.AsNoTracking().AsQueryable();
        if (from is DateTime f) q = q.Where(v => v.VisitDate >= f);
        if (to is DateTime t) q = q.Where(v => v.VisitDate <= t);

        var rows = await q.Join(db.Showrooms.AsNoTracking(),
                v => v.ShowroomId, s => s.Id,
                (v, s) => new { v.VisitDate, s.Name, s.Id, v.VehiclesAttended, v.Amount })
            .GroupBy(x => new { x.Id, x.Name })
            .Select(g => new ShowroomVisitSummaryDto(
                g.Key.Id, g.Key.Name,
                g.Count(), g.Sum(x => x.VehiclesAttended), g.Sum(x => x.Amount)))
            .ToListAsync();

        return rows.OrderByDescending(r => r.TotalAmount).ToList();
    }

    [HttpPost("visits")]
    public async Task<ActionResult<ShowroomVisitDto>> CreateVisit(SaveShowroomVisitRequest req)
    {
        if (req.ShowroomId == Guid.Empty)
            return BadRequest(new { message = "Select a showroom." });
        if (req.VehiclesAttended < 0)
            return BadRequest(new { message = "Vehicles attended cannot be negative." });
        if (req.Amount < 0)
            return BadRequest(new { message = "Amount cannot be negative." });

        var showroom = await db.Showrooms.FindAsync(req.ShowroomId);
        if (showroom is null) return BadRequest(new { message = "Showroom not found." });

        var visit = new ShowroomVisit
        {
            ShowroomId = req.ShowroomId,
            VisitDate = DateTime.SpecifyKind(req.VisitDate.Date, DateTimeKind.Utc),
            TeamSent = req.TeamSent.Trim(),
            VehiclesAttended = req.VehiclesAttended,
            Amount = req.Amount,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim()
        };

        db.ShowroomVisits.Add(visit);
        await db.SaveChangesAsync();

        await audit.LogAsync("ShowroomVisit.Create",
            $"showroom={showroom.Name}; date={visit.VisitDate:yyyy-MM-dd}; amount={visit.Amount:N2}");

        return new ShowroomVisitDto(visit.Id, visit.ShowroomId, showroom.Name,
            visit.VisitDate, visit.TeamSent, visit.VehiclesAttended, visit.Amount, visit.Note);
    }

    [HttpDelete("visits/{id:guid}")]
    public async Task<IActionResult> DeleteVisit(Guid id)
    {
        var visit = await db.ShowroomVisits.FindAsync(id);
        if (visit is null) return NotFound();

        db.ShowroomVisits.Remove(visit);
        await db.SaveChangesAsync();

        await audit.LogAsync("ShowroomVisit.Delete",
            $"showroomId={visit.ShowroomId}; date={visit.VisitDate:yyyy-MM-dd}; amount={visit.Amount:N2}");

        return NoContent();
    }
}
