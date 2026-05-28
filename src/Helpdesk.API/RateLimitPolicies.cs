namespace Helpdesk.API;

internal static class RateLimitPolicies
{
    internal const string Login = "login";
    internal const string PasswordReset = "password-reset";
    internal const string Upload = "upload";
    internal const string RefreshToken = "refresh-token";
}
