using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Contracts;

public record LoginRequest(string Username, string Password);

/// <param name="MustChangePassword">
/// True when the credential was machine-generated (first-run admin, or one rotated off a known
/// default). The client must send the user straight to a change-password prompt: the API refuses
/// every call except the change-own-password endpoint until a new password is set.
/// </param>
public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User, bool MustChangePassword = false);

/// <param name="Permissions">
/// What this user may reach. The role is only the preset that filled it in — this is what the API
/// enforces, and the clients use it to hide screens the user cannot open.
/// </param>
public record UserDto(Guid Id, string FullName, string Username, UserRole Role, bool IsActive,
    Permission Permissions = Permission.None)
{
    public bool Can(Permission p) => true;
}

/// <param name="Permissions">Leave null to use the role's preset.</param>
public record CreateUserRequest(string FullName, string Username, string Password, UserRole Role,
    Permission? Permissions = null);

/// <param name="Permissions">Leave null to keep the user's current permissions.</param>
public record UpdateUserRequest(string FullName, UserRole Role, bool IsActive, string? NewPassword,
    Permission? Permissions = null);

public record ChangeMyPasswordRequest(string OldPassword, string NewPassword);
