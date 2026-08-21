using E6CarSpa.Api.Auth;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Api.Mapping;
using E6CarSpa.Api.Services;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace E6CarSpa.Api.Controllers;

public class AuthController(AppDbContext db, JwtTokenService jwt, AuditService audit) : ApiControllerBase
{
 // Per-account brute-force lockout - complements the per-IP login rate limiter (an attacker
 // rotating IPs still trips this). After this many consecutive wrong passwords the account is
 // locked for the cooldown period; any successful login resets the counter.
 private const int MaxFailedAttempts = 5;
 private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

 // Regenerate on every call so a compromised process memory snapshot can't use the hash
 // as a pre-computed reference across sessions.
 [MethodImpl(MethodImplOptions.NoInlining)]
 private static string FreshDummyHash() => BCrypt.Net.BCrypt.HashPassword("timing-equalizer");

 [HttpPost("login")]
 [AllowAnonymous]
 [EnableRateLimiting("login")]
 public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
 {
 // Reject oversized inputs before passing to BCrypt - hashing very long strings is a CPU DoS.
 if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length > 100 ||
 string.IsNullOrWhiteSpace(req.Password) || req.Password.Length > 200)
 return Unauthorized(new { message = "Invalid username or password." });

 var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);

 // Unknown username (or inactive): generic failure, don't reveal which.
 if (user is null)
 {
 BCrypt.Net.BCrypt.Verify(req.Password, FreshDummyHash());
 await audit.LogAsync("Login.Failed", $"username={req.Username}");
 return Unauthorized(new { message = "Invalid username or password." });
 }

 // Locked account: refuse even with the correct password until the cooldown passes.
 if (user.LockoutEndAt is { } until && until > DateTime.UtcNow)
 {
 await audit.LogAsync("Login.Locked", $"username={user.Username}", user.Id, user.Username);
 return Unauthorized(new
 {
 message = "Account is temporarily locked due to repeated failed attempts. Please try again later."
 });
 }

 if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
 {
 user.FailedLoginCount++;
 if (user.FailedLoginCount >= MaxFailedAttempts)
 {
 user.LockoutEndAt = DateTime.UtcNow.Add(LockoutDuration);
 user.FailedLoginCount = 0;
 }
 await db.SaveChangesAsync();
 await audit.LogAsync("Login.Failed", $"username={user.Username}", user.Id, user.Username);
 return Unauthorized(new { message = "Invalid username or password." });
 }

 // Success: clear any lockout state.
 user.FailedLoginCount = 0;
 user.LockoutEndAt = null;
 user.LastLoginAt = DateTime.UtcNow;
 await db.SaveChangesAsync();
 await audit.LogAsync("Login.Success", null, user.Id, user.Username);

 var (token, expires) = jwt.CreateToken(user);
 return new LoginResponse(token, expires, user.ToDto(), user.MustChangePassword);
 }

 [HttpGet("users")]
 public async Task<ActionResult<List<UserDto>>> GetUsers() =>
 await db.Users.OrderBy(u => u.FullName).Select(u => u.ToDto()).ToListAsync();

 [HttpPost("users")]
 public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest req)
 {
 if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
 return BadRequest(new { message = "Password must be at least 8 characters." });
 if (req.Password.Length > 200)
 return BadRequest(new { message = "Password must not exceed 200 characters." });

 if (await db.Users.AnyAsync(u => u.Username == req.Username))
 return Conflict(new { message = "Username already exists." });

 var user = new User
 {
 FullName = req.FullName,
 Username = req.Username,
 PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
 Role = req.Role,
 Permissions = req.Permissions ?? PermissionPresets.For(req.Role)
 };
 db.Users.Add(user);
 await db.SaveChangesAsync();
 await audit.LogAsync("User.Create",
 $"username={user.Username}; role={user.Role}; permissions={user.Permissions}");
 return user.ToDto();
 }

 [HttpPut("users/{id:guid}")]
 public async Task<ActionResult<UserDto>> UpdateUser(Guid id, UpdateUserRequest req)
 {
 var user = await db.Users.FindAsync(id);
 if (user is null) return NotFound();

 var wasActive = user.IsActive;
 var oldRole = user.Role;
 var oldPermissions = user.Permissions;

 user.FullName = req.FullName;
 user.Role = req.Role;
 user.IsActive = req.IsActive;
 if (req.Permissions is { } perms) user.Permissions = perms;

 var passwordChanged = !string.IsNullOrWhiteSpace(req.NewPassword);
 if (passwordChanged)
 {
 if (req.NewPassword!.Length < 8)
 return BadRequest(new { message = "Password must be at least 8 characters." });
 if (req.NewPassword.Length > 200)
 return BadRequest(new { message = "Password must not exceed 200 characters." });
 user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
 }

 if (passwordChanged || oldRole != req.Role || wasActive != req.IsActive ||
 oldPermissions != user.Permissions)
 user.SecurityStamp = Guid.NewGuid().ToString("N");
 if (passwordChanged)
 {
 user.FailedLoginCount = 0;
 user.LockoutEndAt = null;
 }

 await db.SaveChangesAsync();
 await audit.LogAsync("User.Update",
 $"username={user.Username}; role={user.Role}; active={user.IsActive}; " +
 $"passwordReset={passwordChanged}; permissions={user.Permissions}");
 return user.ToDto();
 }

 [HttpPut("users/me/password")]
 [Authorize]
 public async Task<ActionResult> ChangeMyPassword(ChangeMyPasswordRequest req)
 {
 var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
 if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

 var user = await db.Users.FindAsync(userId);
 if (user is null) return NotFound();

 if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, user.PasswordHash))
 return BadRequest(new { message = "Incorrect old password." });

 if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
 return BadRequest(new { message = "New password must be at least 8 characters." });

 if (req.NewPassword.Length > 200)
 return BadRequest(new { message = "New password must not exceed 200 characters." });

 if (BCrypt.Net.BCrypt.Verify(req.NewPassword, user.PasswordHash))
 return BadRequest(new { message = "The new password must be different from the current one." });

 user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
 user.MustChangePassword = false;
 user.SecurityStamp = Guid.NewGuid().ToString("N");
 await db.SaveChangesAsync();
 await audit.LogAsync("Password.Change", null, user.Id, user.Username);

 return Ok(new { message = "Password updated successfully." });
 }
}
