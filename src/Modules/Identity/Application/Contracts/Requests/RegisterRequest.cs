using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Modules.Identity.Application.Contracts.Requests;

public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MinLength(2), MaxLength(100)]
    public required string Name { get; init; }

    [Required, MinLength(8), MaxLength(72),
     RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&\-_#]).+$",
         ErrorMessage = "Password must have at least 1 uppercase letter, 1 digit, and 1 special character (@$!%*?&-_#)")]
    public required string Password { get; init; }
}
