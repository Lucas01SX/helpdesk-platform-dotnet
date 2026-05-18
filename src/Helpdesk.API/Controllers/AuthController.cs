using Helpdesk.Modules.Identity.Application.Contracts.Requests;
using Helpdesk.Modules.Identity.Application.UseCases;
using Helpdesk.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Helpdesk.API.Controllers;

[Route("api/auth")]
public sealed class AuthController(
    RegisterUseCase registerUseCase,
    LoginUseCase loginUseCase,
    RefreshTokenUseCase refreshTokenUseCase,
    LogoutUseCase logoutUseCase,
    VerifyEmailUseCase verifyEmailUseCase,
    RequestPasswordResetUseCase requestPasswordResetUseCase,
    ResetPasswordUseCase resetPasswordUseCase) : ApiControllerBase
{
    // POST /api/auth/register — creates a new user (Customer role)
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await registerUseCase.ExecuteAsync(request, ct);
        return result.IsSuccess
            ? StatusCode(201, Success(new { userId = result.Value }))
            : BadRequest(Failure(result.Error!));
    }

    // POST /api/auth/sessions — creates a new session (login)
    [HttpPost("sessions")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var enriched = request with
        {
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var result = await loginUseCase.ExecuteAsync(enriched, ct);
        if (result.IsFailure) return Unauthorized(Failure(result.Error!));

        SetRefreshTokenCookie(result.Value!.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Ok(Success(new { accessToken = result.Value.AccessToken }));
    }

    // DELETE /api/auth/sessions/current — deletes the current session (logout)
    [HttpDelete("sessions/current")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(rawToken))
            await logoutUseCase.ExecuteAsync(rawToken, ct);

        Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }

    // PUT /api/auth/sessions/current — replaces the current session (token rotation)
    [HttpPut("sessions/current")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized(Failure(new Error("identity.missing_token", "Refresh token is required")));

        var result = await refreshTokenUseCase.ExecuteAsync(rawToken, ct);
        if (result.IsFailure) return Unauthorized(Failure(result.Error!));

        SetRefreshTokenCookie(result.Value!.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Ok(Success(new { accessToken = result.Value.AccessToken }));
    }

    // PATCH /api/auth/email-verifications — confirms email ownership (token in body, not URL)
    [HttpPatch("email-verifications")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await verifyEmailUseCase.ExecuteAsync(request.Token, ct);
        return result.IsSuccess ? NoContent() : BadRequest(Failure(result.Error!));
    }

    // POST /api/auth/password-resets — creates a new password reset token
    [HttpPost("password-resets")]
    [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] RequestPasswordResetRequest request, CancellationToken ct)
    {
        await requestPasswordResetUseCase.ExecuteAsync(request.Email, ct);
        return NoContent(); // Always 204 — enumeration protection
    }

    // PATCH /api/auth/password-resets — applies a password reset (token in body, not URL)
    [HttpPatch("password-resets")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await resetPasswordUseCase.ExecuteAsync(request, ct);
        return result.IsSuccess ? NoContent() : BadRequest(Failure(result.Error!));
    }

    private void SetRefreshTokenCookie(string token, DateTime expiresAt)
    {
        Response.Cookies.Append("refreshToken", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/api/auth"
        });
    }

}
