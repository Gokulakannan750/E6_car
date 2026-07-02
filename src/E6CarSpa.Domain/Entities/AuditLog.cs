namespace E6CarSpa.Domain.Entities;

/// <summary>
/// An immutable record of a security- or money-relevant action (logins, user management,
/// price/catalogue edits, settings changes). The event time is <see cref="BaseEntity.CreatedAt"/>.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Dotted action code, e.g. "Login.Success", "User.Create", "Service.Update".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The acting user's id, or null for anonymous/failed-login events.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Username snapshot at the time of the action (survives later renames/deletes).</summary>
    public string? Username { get; set; }

    /// <summary>Human-readable specifics, e.g. "username=worker1; role=Worker".</summary>
    public string? Detail { get; set; }

    /// <summary>Remote IP the request came from (honours X-Forwarded-For behind the proxy).</summary>
    public string? IpAddress { get; set; }
}
