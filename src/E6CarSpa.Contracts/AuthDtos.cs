using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Contracts;

public record LoginRequest(string Username, string Password);

/// <param name="MustChangePassword">
/// True when the credential was machine-generated (first-run admin, or one rotated off a known
/// default). The client must send the user straight to a change-password prompt: the API refuses
/// every call except the change-own-password endpoint until a new password is set.
/// </param>
public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User, bool MustChangePassword = false);

public record UserDto(Guid Id, string FullName, string Username, UserRole Role, bool IsActive);

public record CreateUserRequest(string FullName, string Username, string Password, UserRole Role);

public record UpdateUserRequest(string FullName, UserRole Role, bool IsActive, string? NewPassword);

public record ChangeMyPasswordRequest(string OldPassword, string NewPassword);
