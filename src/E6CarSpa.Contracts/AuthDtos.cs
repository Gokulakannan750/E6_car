using E6CarSpa.Domain.Enums;

namespace E6CarSpa.Contracts;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

public record UserDto(Guid Id, string FullName, string Username, UserRole Role, bool IsActive);

public record CreateUserRequest(string FullName, string Username, string Password, UserRole Role);

public record UpdateUserRequest(string FullName, UserRole Role, bool IsActive, string? NewPassword);

public record ChangeMyPasswordRequest(string OldPassword, string NewPassword);
