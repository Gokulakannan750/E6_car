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
/// CRUD for showroom master records.
/// </summary>
[ApiController]
[Route("api/showrooms")]
public class ShowroomsController(AppDbContext db, AuditService audit) : ApiControllerBase
{
 /// <summary>All showrooms, newest last. Includes inactive when ?includeInactive is true.</summary>
 [HttpGet]
 public async Task<ActionResult<List<ShowroomDto>>> List([FromQuery] bool includeInactive = false)
 {
 try
 {
 var q = db.Showrooms.AsNoTracking().AsQueryable();
 if (!includeInactive) q = q.Where(s => s.IsActive);
 return await q.OrderByDescending(s => s.CreatedAt)
 .Select(s => new ShowroomDto(s.Id, s.Name, s.Address, s.Phone, s.ContactPerson,
 s.Notes, s.IsActive, s.CreatedAt))
 .ToListAsync();
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not load showrooms",
 detail: $"{ex.GetType().Name}: {ex.Message}",
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 /// <summary>Active showrooms in name order — for pickers.</summary>
 [HttpGet("picker")]
 public async Task<ActionResult<List<ShowroomDto>>> Picker()
 {
 return await db.Showrooms.AsNoTracking()
 .Where(s => s.IsActive)
 .OrderBy(s => s.Name)
 .Select(s => new ShowroomDto(s.Id, s.Name, s.Address, s.Phone, s.ContactPerson,
 s.Notes, s.IsActive, s.CreatedAt))
 .ToListAsync();
 }

 [HttpPost]
 public async Task<ActionResult<ShowroomDto>> Create(SaveShowroomRequest req)
 {
 if (string.IsNullOrWhiteSpace(req.Name))
 return BadRequest(new { message = "Enter the showroom name." });
 if (string.IsNullOrWhiteSpace(req.Address))
 return BadRequest(new { message = "Enter the showroom address." });

 try
 {
 var showroom = new Showroom
 {
 Name = req.Name.Trim(),
 Address = req.Address.Trim(),
 Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
 ContactPerson = string.IsNullOrWhiteSpace(req.ContactPerson) ? null : req.ContactPerson.Trim(),
 Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim()
 };

 db.Showrooms.Add(showroom);
 await db.SaveChangesAsync();

 var dto = new ShowroomDto(showroom.Id, showroom.Name, showroom.Address,
 showroom.Phone, showroom.ContactPerson, showroom.Notes,
 showroom.IsActive, showroom.CreatedAt);
 await audit.LogAsync("Showroom.Create", $"name={showroom.Name}");
 return CreatedAtAction(nameof(List), new { id = showroom.Id }, dto);
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not create showroom",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 [HttpPut("{id:guid}")]
 public async Task<ActionResult<ShowroomDto>> Update(Guid id, SaveShowroomRequest req)
 {
 if (string.IsNullOrWhiteSpace(req.Name))
 return BadRequest(new { message = "Enter the showroom name." });
 if (string.IsNullOrWhiteSpace(req.Address))
 return BadRequest(new { message = "Enter the showroom address." });

 try
 {
 var showroom = await db.Showrooms.FindAsync(id);
 if (showroom is null) return NotFound();

 showroom.Name = req.Name.Trim();
 showroom.Address = req.Address.Trim();
 showroom.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
 showroom.ContactPerson = string.IsNullOrWhiteSpace(req.ContactPerson) ? null : req.ContactPerson.Trim();
 showroom.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
 showroom.UpdatedAt = DateTime.UtcNow;
 await db.SaveChangesAsync();

 await audit.LogAsync("Showroom.Update", $"name={showroom.Name}");
 return new ShowroomDto(showroom.Id, showroom.Name, showroom.Address,
 showroom.Phone, showroom.ContactPerson, showroom.Notes,
 showroom.IsActive, showroom.CreatedAt);
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not update showroom",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 [HttpDelete("{id:guid}")]
 public async Task<IActionResult> Deactivate(Guid id)
 {
 try
 {
 var showroom = await db.Showrooms.FindAsync(id);
 if (showroom is null) return NotFound();

 showroom.IsActive = false;
 showroom.UpdatedAt = DateTime.UtcNow;
 await db.SaveChangesAsync();
 await audit.LogAsync("Showroom.Deactivate", $"name={showroom.Name}");
 return NoContent();
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not deactivate showroom",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 [HttpPost("{id:guid}/restore")]
 public async Task<IActionResult> Restore(Guid id)
 {
 try
 {
 var showroom = await db.Showrooms.FindAsync(id);
 if (showroom is null) return NotFound();

 showroom.IsActive = true;
 showroom.UpdatedAt = DateTime.UtcNow;
 await db.SaveChangesAsync();
 await audit.LogAsync("Showroom.Restore", $"name={showroom.Name}");
 return NoContent();
 }
 catch (Exception ex)
 {
 return Problem(title: "Could not restore showroom",
 detail: ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }
}
