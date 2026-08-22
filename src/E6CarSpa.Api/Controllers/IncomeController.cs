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

/// <summary>Non-invoice income entries (tips, miscellaneous, part sale, etc.).</summary>
public class IncomeController(AppDbContext db, AuditService audit) : ApiControllerBase
{
 /// <param name="page">1-based page index.</param>
 /// <param name="pageSize">Rows per page (capped at 500).</param>
 [HttpGet]
 public async Task<ActionResult<List<IncomeDto>>> List(
 [FromQuery] string? source, [FromQuery] bool includeDeleted = false,
 [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
 {
 try
 {
 var q = db.Income.AsNoTracking().AsQueryable();
 if (!includeDeleted) q = q.Where(i => i.DeletedAt == null);
 if (!string.IsNullOrWhiteSpace(source))
 {
 var s = source.Trim().ToLower();
 q = q.Where(i => i.Source.ToLower().Contains(s));
 }

 pageSize = Math.Clamp(pageSize, 1, 500);
 page = Math.Max(page, 1);

 var total = await q.CountAsync();
 var items = await q.OrderByDescending(i => i.IncomeDate).ThenByDescending(i => i.CreatedAt)
 .Skip((page - 1) * pageSize)
 .Take(pageSize)
 .Select(i => new IncomeDto(i.Id, i.Source, i.Amount, i.IncomeDate, i.Note,
 i.DeletedAt, i.DeletedByUsername))
 .ToListAsync();

 Response.Headers.Append("X-Total-Count", total.ToString());
 return items;
 }
 catch (Exception ex)
 {
 return Problem(
 title: "Could not load income entries",
 detail: ex.InnerException?.Message ?? ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 /// <summary>Total income, grouped by source.</summary>
 [HttpGet("summary")]
 public async Task<ActionResult<List<IncomeSummaryDto>>> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
 {
 try
 {
 var q = db.Income.AsNoTracking().Where(i => i.DeletedAt == null);
 if (from.HasValue) q = q.Where(i => i.IncomeDate >= from.Value.Date);
 if (to.HasValue) q = q.Where(i => i.IncomeDate < to.Value.Date.AddDays(1));

 var rows = await q.GroupBy(i => i.Source)
 .Select(g => new IncomeSummaryDto(g.Key, g.Sum(x => x.Amount), g.Count()))
 .ToListAsync();
 return rows.OrderByDescending(r => r.TotalAmount).ToList();
 }
 catch (Exception ex)
 {
 return Problem(
 title: "Could not load summary",
 detail: ex.InnerException?.Message ?? ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 /// <summary>Total income for a date range (used by the dashboard).</summary>
 [HttpGet("total")]
 public async Task<ActionResult<decimal>> Total([FromQuery] DateTime from, [FromQuery] DateTime to)
 {
 try
 {
 var q = db.Income.AsNoTracking().Where(i => i.DeletedAt == null && i.IncomeDate >= from.Date && i.IncomeDate < to.Date.AddDays(1));
 var total = await q.SumAsync(i => i.Amount);
 return Ok(total);
 }
 catch (Exception ex)
 {
 return Problem(
 title: "Could not load total",
 detail: ex.InnerException?.Message ?? ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 [HttpPost]
 public async Task<ActionResult<IncomeDto>> Create(SaveIncomeRequest req)
 {
 if (string.IsNullOrWhiteSpace(req.Source))
 return BadRequest(new { message = "Enter the income source." });
 if (req.Amount <= 0)
 return BadRequest(new { message = "Amount must be greater than zero." });

 try
 {
 var income = new Income
 {
 Source = req.Source.Trim(),
 Amount = req.Amount,
 IncomeDate = DateTime.SpecifyKind(req.IncomeDate.Date, DateTimeKind.Utc),
 Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
 RecordedByUserId = CurrentUserId
 };

 db.Income.Add(income);
 await db.SaveChangesAsync();

 var dto = new IncomeDto(income.Id, income.Source, income.Amount, income.IncomeDate, income.Note);
 await audit.LogAsync("Income.Create", $"source={income.Source}; amount={income.Amount:0.##}; date={income.IncomeDate:yyyy-MM-dd}");
 return dto;
 }
 catch (Exception ex)
 {
 return Problem(
 title: "Could not record income",
 detail: ex.InnerException?.Message ?? ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }

 /// <summary>Soft-delete: marks the entry obsolete. Historical totals exclude it.</summary>
 [HttpDelete("{id:guid}")]
 public async Task<IActionResult> Delete(Guid id)
 {
 try
 {
 var income = await db.Income.FindAsync(id);
 if (income is null) return NotFound();
 if (income.DeletedAt is not null) return NoContent();

 income.DeletedAt = DateTime.UtcNow;
 income.DeletedByUserId = CurrentUserId;
 income.DeletedByUsername = CurrentUsername;
 income.UpdatedAt = DateTime.UtcNow;

 await db.SaveChangesAsync();
 await audit.LogAsync("Income.Delete", $"source={income.Source}; amount={income.Amount:0.##}");

 return NoContent();
 }
 catch (Exception ex)
 {
 return Problem(
 title: "Could not delete income",
 detail: ex.InnerException?.Message ?? ex.Message,
 statusCode: StatusCodes.Status500InternalServerError);
 }
 }
}
