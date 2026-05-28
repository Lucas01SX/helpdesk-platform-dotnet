using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Helpdesk.Tests.Integration.Infrastructure;

namespace Helpdesk.Tests.Integration.Identity;

// Uses HelpdeskRateLimitWebAppFactory (not HelpdeskWebAppFactory) so that the rate
// limiter is active — HelpdeskWebAppFactory sets UseEnvironment("Test") which disables it.
// Same collection as AuthEndpointsTests — serializes factory startup to avoid Serilog race.
[Collection("auth-integration")]
public sealed class AuthRateLimitTests(HelpdeskRateLimitWebAppFactory factory)
    : IClassFixture<HelpdeskRateLimitWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── POST /api/auth/sessions — 5 requests / minute / IP ───────────────────

    [Fact]
    public async Task Sessions_Post_Should_Return_429_After_Exceeding_Login_Rate_Limit()
    {
        var payload = new { email = "ratelimit@example.com", password = "AnyPass1@" };

        HttpResponseMessage? lastResponse = null;

        // Send 6 requests — the 6th must be rate-limited (limit is 5/min)
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await _client.PostAsJsonAsync("/api/auth/sessions", payload);
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var body = await lastResponse.Content.ReadAsStringAsync();
        body.Should().Contain("rate_limit_exceeded");
    }

    // ── POST /api/auth/password-resets — 3 requests / hour / IP ─────────────

    [Fact]
    public async Task PasswordResets_Post_Should_Return_429_After_Exceeding_Rate_Limit()
    {
        var payload = new { email = "resetlimit@example.com" };

        HttpResponseMessage? lastResponse = null;

        // Send 4 requests — the 4th must be rate-limited (limit is 3/hour)
        for (var i = 0; i < 4; i++)
        {
            lastResponse = await _client.PostAsJsonAsync("/api/auth/password-resets", payload);
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var body = await lastResponse.Content.ReadAsStringAsync();
        body.Should().Contain("rate_limit_exceeded");
    }

    // ── PUT /api/auth/sessions/current — 20 requests / minute / IP ──────────

    [Fact]
    public async Task SessionsPut_Should_Return_429_After_Exceeding_RefreshToken_Rate_Limit()
    {
        HttpResponseMessage? lastResponse = null;

        // Rate limiter runs before auth — unauthenticated requests still count.
        // Send 21 requests; the 21st must be rate-limited (limit is 20/min).
        for (var i = 0; i < 21; i++)
        {
            lastResponse = await _client.PutAsync("/api/auth/sessions/current", null);
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var body = await lastResponse.Content.ReadAsStringAsync();
        body.Should().Contain("rate_limit_exceeded");
    }
}
