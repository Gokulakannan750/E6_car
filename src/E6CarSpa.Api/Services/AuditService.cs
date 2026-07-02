using System.Security.Claims;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;

namespace E6CarSpa.Api.Services;

/// <summary>
/// Writes immutable audit records for security- and money-relevant actions.
/// Best-effort: an audit failure is logged but never breaks the business operation
/// (the audit table lives in the same DB, so if it's down the operation failed too).
/// </summary>
public class AuditService(AppDbContext db, IHttpContextAccessor http, ILogger<AuditService> logger)
{
    /// <summary>
    /// Record an action. Actor id/username default to the authenticated caller; pass them
    /// explicitly for events where there is no caller principal yet (e.g. login attempts).
    /// </summary>
    public async Task LogAsync(string action, string? detail = null, Guid? userId = null, string? username = null)
    {
        try
        {
            var ctx = http.HttpContext;
            var principal = ctx?.User;

            if (userId is null && Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                userId = id;
            username ??= principal?.FindFirstValue(ClaimTypes.Name);

            db.AuditLogs.Add(new AuditLog
            {
                Action = action,
                UserId = userId,
                Username = Trim(username, 50),
                Detail = Trim(detail, 500),
                IpAddress = Trim(ctx?.Connection.RemoteIpAddress?.ToString(), 64)
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never let auditing failure surface to the user or roll back their action.
            logger.LogWarning(ex, "Failed to write audit log for action {Action}", action);
        }
    }

    private static string? Trim(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
