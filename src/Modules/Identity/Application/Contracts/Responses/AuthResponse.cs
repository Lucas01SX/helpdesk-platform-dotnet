namespace Helpdesk.Modules.Identity.Application.Contracts.Responses;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
