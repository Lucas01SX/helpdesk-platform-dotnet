using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Modules.Identity.Application.Contracts.Requests;

public sealed record RequestPasswordResetRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }
}
