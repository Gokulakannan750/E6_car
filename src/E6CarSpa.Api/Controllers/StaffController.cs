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

/// <summary>
/// Floor-worker master records (add, rename, deactivate, restore).
/// The staff picker in the advances/salary screens reads from here.
/// </summary>
[ApiController]
[Route("api/staff")]
[RequirePermission(Permission.StaffManage)]
public class StaffController(AppDbContext db, AuditService audit) : ApiControllerBase
{
    /// <summary>All staff, newest last. Inactive entries are included when ?includeInactive is on.</summary>
    [HttpGet]
    public async Task<ActionResult<List<StaffDto>>> List([FromQuery] bool includeInactive = false)
    {
        try
        {
            var q = db.Staff.AsNoTracking().AsQueryable();
            if (!includeInactive) q = q.Where(s => s.IsActive);
            return await q.OrderByDescending(s => s.CreatedAt)
                .Select(s => new StaffDto(s.Id, s.FullName, s.IsActive))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not load staff list",
                detail: $"Database error: {ex.GetType().Name}: {ex.Message}",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>Active staff in name order — for pickers (advances, salaries).</summary>
    [HttpGet("picker")]
    public async Task<ActionResult<List<StaffDto>>> Picker()
    {
        return await db.Staff.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.FullName)
            .Select(s => new StaffDto(s.Id, s.FullName, s.IsActive))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<StaffDto>> Create(SaveStaffRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { message = "Enter the staff member's name." });

        try
        {
            var staff = new Staff { FullName = req.FullName.Trim() };
            db.Staff.Add(staff);
            await db.SaveChangesAsync();

            var dto = new StaffDto(staff.Id, staff.FullName, staff.IsActive);
            await audit.LogAsync("Staff.Create", $"name={staff.FullName}");
            return CreatedAtAction(nameof(List), new { id = staff.Id }, dto);
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not create staff member",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffDto>> Update(Guid id, SaveStaffRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new { message = "Enter the staff member's name." });

        try
        {
            var staff = await db.Staff.FindAsync(id);
            if (staff is null) return NotFound();

            staff.FullName = req.FullName.Trim();
            staff.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await audit.LogAsync("Staff.Update", $"name={staff.FullName}");
            return new StaffDto(staff.Id, staff.FullName, staff.IsActive);
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not update staff member",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var staff = await db.Staff.FindAsync(id);
            if (staff is null) return NotFound();

            staff.IsActive = false;
            staff.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.LogAsync("Staff.Deactivate", $"name={staff.FullName}");
            return NoContent();
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not deactivate staff member",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        try
        {
            var staff = await db.Staff.FindAsync(id);
            if (staff is null) return NotFound();

            staff.IsActive = true;
            staff.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.LogAsync("Staff.Restore", $"name={staff.FullName}");
            return NoContent();
        }
        catch (Exception ex)
        {
            return Problem(
                title: "Could not restore staff member",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
