using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Helpdesk.Modules.Identity.Application.Interfaces;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Shared.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Modules.Identity.Infrastructure.Security;

internal sealed class JwtTokenService(IOptions<JwtSettings> options, IDateTimeProvider clock) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

    public string GenerateAccessToken(Guid userId, string email, UserRole role, Guid sessionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("sessionId", sessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var now = clock.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
