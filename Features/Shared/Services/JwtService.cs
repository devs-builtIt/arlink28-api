using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Helpers.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Arlink28.Api.Features.Shared.Services;

public class JwtService(IOptions<AppSettings> appSettings) : IJwtService, IScoped
{
    private readonly AppSettings _settings = appSettings.Value;

    public (string Token, DateTime ExpiresAt) IssueToken(Staff staff)
    {
        var expiresAt = DateTime.UtcNow.AddHours(8);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new Claim(ClaimTypes.Name, staff.Username),
            new Claim(ClaimTypes.Email, staff.Email),
            new Claim(ClaimTypes.Role, staff.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _settings.ValidIssuer,
            audience: _settings.ValidAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
