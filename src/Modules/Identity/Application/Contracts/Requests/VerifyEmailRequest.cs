using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Modules.Identity.Application.Contracts.Requests;

public sealed record VerifyEmailRequest
{
    [Required]
    public required string Token { get; init; }
}
