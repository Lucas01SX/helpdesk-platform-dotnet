using Helpdesk.Shared.Errors;

namespace Helpdesk.Modules.Identity.Application.Errors;

public static class IdentityErrors
{
    public static readonly Error InvalidCredentials =
        new("identity.invalid_credentials", "Invalid credentials");

    public static readonly Error EmailAlreadyRegistered =
        new("identity.email_already_registered", "Email already in use");

    public static readonly Error EmailNotVerified =
        new("identity.email_not_verified", "Please verify your email before logging in");

    public static readonly Error InvalidOrExpiredToken =
        new("identity.invalid_or_expired_token", "Token is invalid or has expired");

    public static readonly Error SessionRevoked =
        new("identity.session_revoked", "Your session has been invalidated due to a security event");
}
