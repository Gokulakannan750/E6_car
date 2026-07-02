using E6CarSpa.Contracts;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;

/// <summary>Read-only access to the audit trail. Admin only.</summary>
[Authorize(Roles = "Admin")]
public class AuditController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Most recent audit entries, newest first. Optionally filter by action prefix.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> GetRecent(
        [FromQuery] string? action = null, [FromQuery] int take = 200)
    {
        take = Math.Clamp(take, 1, 1000);
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(a => a.Action.StartsWith(action));

        return await q.OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new AuditLogDto(a.CreatedAt, a.Action, a.Username, a.Detail, a.IpAddress))
            .ToListAsync();
    }
}
