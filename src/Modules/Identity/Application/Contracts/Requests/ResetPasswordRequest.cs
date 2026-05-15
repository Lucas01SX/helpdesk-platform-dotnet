using System.ComponentModel.DataAnnotations;

namespace Helpdesk.Modules.Identity.Application.Contracts.Requests;

public sealed record ResetPasswordRequest
{
    [Required]
    public required string Token { get; init; }

    [Required, MinLength(8), MaxLength(72),
     RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&\-_#]).+$",
         ErrorMessage = "Password must have at least 1 uppercase letter, 1 digit, and 1 special character (@$!%*?&-_#)")]
    public required string NewPassword { get; init; }
}
