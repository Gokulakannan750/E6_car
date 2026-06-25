using E6CarSpa.Api.Auth;
using E6CarSpa.Api.Mapping;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Controllers;

public class AuthController(AppDbContext db, JwtTokenService jwt) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        // Reject oversized inputs before passing to BCrypt — hashing very long strings is a CPU DoS.
        if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length > 100 ||
            string.IsNullOrWhiteSpace(req.Password) || req.Password.Length > 200)
            return Unauthorized(new { message = "Invalid username or password." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password." });

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var (token, expires) = jwt.CreateToken(user);
        return new LoginResponse(token, expires, user.ToDto());
    }

    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<UserDto>>> GetUsers() =>
        await db.Users.OrderBy(u => u.FullName).Select(u => u.ToDto()).ToListAsync();

    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
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
            Role = req.Role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.ToDto();
    }

    [HttpPut("users/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, UpdateUserRequest req)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.FullName = req.FullName;
        user.Role = req.Role;
        user.IsActive = req.IsActive;
        if (!string.IsNullOrWhiteSpace(req.NewPassword))
        {
            if (req.NewPassword.Length < 8)
                return BadRequest(new { message = "Password must be at least 8 characters." });
            if (req.NewPassword.Length > 200)
                return BadRequest(new { message = "Password must not exceed 200 characters." });
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        }

        await db.SaveChangesAsync();
        return user.ToDto();
    }
}
