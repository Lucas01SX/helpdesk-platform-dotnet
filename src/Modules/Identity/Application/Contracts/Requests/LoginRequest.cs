using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Modules.Identity.Application.Contracts.Requests;

public sealed record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }

    public string? UserAgent { get; init; }
    public string? IpAddress { get; init; }
}
