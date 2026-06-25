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
}
