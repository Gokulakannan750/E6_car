using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Domain.Entities;

/// <summary>A staff member who logs into the application.</summary>
public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Worker;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // ----- Brute-force lockout -----
    /// <summary>Consecutive failed login attempts; reset to 0 on any successful login.</summary>
    public int FailedLoginCount { get; set; }
    /// <summary>When set and in the future, the account is temporarily locked and cannot log in.</summary>
    public DateTime? LockoutEndAt { get; set; }

    // ----- Token revocation -----
    /// <summary>
    /// Opaque value embedded in every issued JWT. Rotating it (on password change, role change,
    /// or deactivation) instantly invalidates all previously issued tokens for this user.
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    // ----- Forced password reset -----
    /// <summary>
    /// When true the account may authenticate but do nothing except change its own password —
    /// every other endpoint is refused. Set on machine-generated credentials (the first-run admin,
    /// or an account whose password was auto-rotated off a known default) so a password the
    /// operator did not choose can never quietly become the permanent one.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
