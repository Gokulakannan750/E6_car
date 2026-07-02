namespace E6CarSpa.Contracts;

/// <summary>A single audit-trail entry (read-only, Admin-viewable).</summary>
public record AuditLogDto(
    DateTime At,
    string Action,
    string? Username,
    string? Detail,
    string? IpAddress);
