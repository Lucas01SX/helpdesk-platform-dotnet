using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Modules.Identity.Infrastructure.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public required string SecretKey { get; init; }

    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    public int ExpiryMinutes { get; init; } = 15;
}
