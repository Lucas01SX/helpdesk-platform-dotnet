using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Helpdesk.Tests.Integration.Infrastructure;

namespace Helpdesk.Tests.Integration.Identity;

// Same collection as AuthRateLimitTests — serializes factory startup so the two
// WebApplicationFactory instances don't race on Serilog's static Log.Logger.
[Collection("auth-integration")]
public sealed class AuthEndpointsTests(HelpdeskWebAppFactory factory)
    : IClassFixture<HelpdeskWebAppFactory>
{
    // Cookie-aware client so Set-Cookie responses (refreshToken) are sent on follow-up requests.
    private readonly HttpClient _client = factory.CreateClientWithCookies();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task RegisterAndVerifyAsync(string email, string name = "Test User",
        string password = "Secret1@pass")
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new { email, name, password });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created, $"registration of {email} should succeed");

        var token = factory.EmailService.GetVerificationToken(email);
        token.Should().NotBeNullOrEmpty($"verification token for {email} should have been captured by InMemoryEmailService");

        var verifyResponse = await _client.PatchAsJsonAsync("/api/auth/email-verifications", new { token });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, $"email verification for {email} should succeed");
    }

    private async Task<string> LoginAsync(string email, string password = "Secret1@pass")
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sessions", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    // ── POST /api/auth/register ───────────────────────────────────────────────

    [Fact]
    public async Task Register_Should_Return_201_With_UserId()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "alice@example.com",
            name = "Alice",
            password = "Secret1@pass"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("userId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_Should_Return_400_When_Email_Already_Registered()
    {
        var payload = new { email = "bob@example.com", name = "Bob", password = "Secret1@pass" };
        await _client.PostAsJsonAsync("/api/auth/register", payload);

        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Return_400_When_Password_Does_Not_Meet_Requirements()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "weak@example.com",
            name = "Weak",
            password = "password" // no uppercase, no digit, no special char
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PATCH /api/auth/email-verifications ───────────────────────────────────

    [Fact]
    public async Task EmailVerifications_Patch_Should_Return_400_With_Invalid_Token()
    {
        var response = await _client.PatchAsJsonAsync("/api/auth/email-verifications", new
        {
            token = "bogus-verification-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EmailVerifications_Patch_Should_Return_204_With_Valid_Token()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "dave@example.com",
            name = "Dave",
            password = "Secret1@pass"
        });

        var token = factory.EmailService.GetVerificationToken("dave@example.com")!;
        token.Should().NotBeNullOrEmpty();

        var response = await _client.PatchAsJsonAsync("/api/auth/email-verifications", new { token });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task EmailVerifications_Patch_Should_Return_400_When_Token_Already_Used()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "eve@example.com",
            name = "Eve",
            password = "Secret1@pass"
        });

        var token = factory.EmailService.GetVerificationToken("eve@example.com")!;
        await _client.PatchAsJsonAsync("/api/auth/email-verifications", new { token }); // first use

        var response = await _client.PatchAsJsonAsync("/api/auth/email-verifications", new { token }); // reuse

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/auth/sessions (login) ──────────────────────────────────────

    [Fact]
    public async Task Sessions_Post_Should_Return_401_Before_Email_Is_Verified()
    {
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "carol@example.com",
            name = "Carol",
            password = "Secret1@pass"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/sessions", new
        {
            email = "carol@example.com",
            password = "Secret1@pass"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sessions_Post_Should_Return_Same_Error_For_Wrong_Password_And_Unknown_Email()
    {
        var wrongPasswordResponse = await _client.PostAsJsonAsync("/api/auth/sessions", new
        {
            email = "nonexistent@example.com",
            password = "AnyPassword1@"
        });

        var unknownEmailResponse = await _client.PostAsJsonAsync("/api/auth/sessions", new
        {
            email = "alsonone@example.com",
            password = "AnotherPass1@"
        });

        wrongPasswordResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmailResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body1 = await wrongPasswordResponse.Content.ReadAsStringAsync();
        var body2 = await unknownEmailResponse.Content.ReadAsStringAsync();

        // Both must return the same error code — enumeration protection
        body1.Should().Contain("identity.invalid_credentials");
        body2.Should().Contain("identity.invalid_credentials");
    }

    [Fact]
    public async Task Sessions_Post_Should_Return_200_With_AccessToken_For_Verified_User()
    {
        await RegisterAndVerifyAsync("frank@example.com", "Frank");

        var response = await _client.PostAsJsonAsync("/api/auth/sessions", new
        {
            email = "frank@example.com",
            password = "Secret1@pass"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        // Refresh token cookie must be set as HttpOnly
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c =>
            c.Contains("refreshToken") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    // ── DELETE /api/auth/sessions/current (logout) ───────────────────────────

    [Fact]
    public async Task Sessions_Delete_Should_Return_204_Even_Without_Cookie()
    {
        var response = await _client.DeleteAsync("/api/auth/sessions/current");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Sessions_Delete_Should_Return_204_And_Revoke_Active_Session()
    {
        await RegisterAndVerifyAsync("grace@example.com", "Grace");
        await LoginAsync("grace@example.com"); // sets cookie on _client

        var response = await _client.DeleteAsync("/api/auth/sessions/current");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // After logout, refresh should return 401
        var refreshResponse = await _client.PutAsync("/api/auth/sessions/current", content: null);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/auth/sessions/current (refresh) ─────────────────────────────

    [Fact]
    public async Task Sessions_Put_Should_Return_401_Without_Refresh_Cookie()
    {
        var response = await _client.PutAsync("/api/auth/sessions/current", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sessions_Put_Should_Return_200_With_New_Token_After_Login()
    {
        await RegisterAndVerifyAsync("henry@example.com", "Henry");
        await LoginAsync("henry@example.com"); // sets cookie on _client

        var response = await _client.PutAsync("/api/auth/sessions/current", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        // New refresh token cookie must be set
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c =>
            c.Contains("refreshToken") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sessions_Put_Should_Return_401_After_Token_Reuse_And_Revoke_Family()
    {
        await RegisterAndVerifyAsync("igor@example.com", "Igor");

        // Use a raw client (no cookie jar) so we can capture and reuse the old token manually.
        // factory.Server.CreateHandler() returns the in-process pipeline handler without cookie management.
        using var rawClient = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };

        // Login and capture the refresh token from the Set-Cookie header
        var loginMsg = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sessions");
        loginMsg.Content = JsonContent.Create(new { email = "igor@example.com", password = "Secret1@pass" });
        var loginResponse = await rawClient.SendAsync(loginMsg);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var oldRefreshToken = loginResponse.Headers.GetValues("Set-Cookie")
            .First(c => c.Contains("refreshToken="))
            .Split(';')[0].Split('=', 2)[1];

        // First refresh with the old token — rotates: T1 is revoked, T2 is issued
        var refresh1 = new HttpRequestMessage(HttpMethod.Put, "/api/auth/sessions/current");
        refresh1.Headers.Add("Cookie", $"refreshToken={oldRefreshToken}");
        var refresh1Response = await rawClient.SendAsync(refresh1);
        refresh1Response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reuse T1 (the now-revoked token) — must trigger family invalidation → 401 session_revoked
        var reuseMsg = new HttpRequestMessage(HttpMethod.Put, "/api/auth/sessions/current");
        reuseMsg.Headers.Add("Cookie", $"refreshToken={oldRefreshToken}");
        var reuseResponse = await rawClient.SendAsync(reuseMsg);

        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await reuseResponse.Content.ReadAsStringAsync();
        body.Should().Contain("identity.session_revoked");
    }

    // ── POST /api/auth/password-resets (request) ─────────────────────────────

    [Fact]
    public async Task PasswordResets_Post_Should_Always_Return_204_Regardless_Of_Email()
    {
        var knownEmailResponse = await _client.PostAsJsonAsync("/api/auth/password-resets", new
        {
            email = "existing@example.com"
        });

        var unknownEmailResponse = await _client.PostAsJsonAsync("/api/auth/password-resets", new
        {
            email = "doesnotexist@example.com"
        });

        // Both 204 — enumeration protection (never reveal whether email exists)
        knownEmailResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        unknownEmailResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── PATCH /api/auth/password-resets (apply) ──────────────────────────────

    [Fact]
    public async Task PasswordResets_Patch_Should_Return_400_With_Invalid_Token()
    {
        var response = await _client.PatchAsJsonAsync("/api/auth/password-resets", new
        {
            token = "invalid-token",
            newPassword = "NewSecret1@pass"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PasswordResets_Patch_Should_Return_204_And_Allow_Login_With_New_Password()
    {
        await RegisterAndVerifyAsync("julia@example.com", "Julia");

        // Request reset
        await _client.PostAsJsonAsync("/api/auth/password-resets", new { email = "julia@example.com" });
        var resetToken = factory.EmailService.GetResetToken("julia@example.com")!;
        resetToken.Should().NotBeNullOrEmpty();

        // Apply reset
        var resetResponse = await _client.PatchAsJsonAsync("/api/auth/password-resets", new
        {
            token = resetToken,
            newPassword = "NewSecret1@pass"
        });

        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Old password no longer works
        var oldLoginResponse = await _client.PostAsJsonAsync("/api/auth/sessions",
            new { email = "julia@example.com", password = "Secret1@pass" });
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // New password works
        var newLoginResponse = await _client.PostAsJsonAsync("/api/auth/sessions",
            new { email = "julia@example.com", password = "NewSecret1@pass" });
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PasswordResets_Patch_Should_Return_400_When_Token_Already_Used()
    {
        await RegisterAndVerifyAsync("kevin@example.com", "Kevin");

        await _client.PostAsJsonAsync("/api/auth/password-resets", new { email = "kevin@example.com" });
        var resetToken = factory.EmailService.GetResetToken("kevin@example.com")!;

        await _client.PatchAsJsonAsync("/api/auth/password-resets", new
        {
            token = resetToken,
            newPassword = "NewSecret1@pass"
        });

        // Reuse the same token
        var response = await _client.PatchAsJsonAsync("/api/auth/password-resets", new
        {
            token = resetToken,
            newPassword = "AnotherNew1@pass"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
