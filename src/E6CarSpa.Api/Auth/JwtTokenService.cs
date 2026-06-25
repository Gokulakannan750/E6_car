using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using E6CarSpa.Api.Config;
using E6CarSpa.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace E6CarSpa.Api.Auth;

public class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public (string token, DateTime expiresAt) CreateToken(User user)
    {
        var expires = DateTime.UtcNow.AddHours(_opt.ExpiryHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("fullName", user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
