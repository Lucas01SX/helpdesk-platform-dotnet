using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Helpdesk.Tests.Integration.Infrastructure;

namespace Helpdesk.Tests.Integration.Observability;

[Collection("auth-integration")]
public sealed class ObservabilityTests(HelpdeskWebAppFactory factory)
    : IClassFixture<HelpdeskWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Correlation ID Middleware ─────────────────────────────────────────────

    [Fact]
    public async Task CorrelationId_Should_Echo_Incoming_Header()
    {
        var id = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", id);

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.First().Should().Be(id);
    }

    [Fact]
    public async Task CorrelationId_Should_Generate_When_Not_Provided()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.First().Should().NotBeNullOrEmpty();
        Guid.TryParse(values!.First(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task CorrelationId_Should_Reject_Oversized_Header_And_Generate_New_Id()
    {
        var oversized = new string('x', 65);
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", oversized);

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        var returned = values!.First();
        returned.Should().NotBe(oversized);
        returned.Length.Should().BeLessThanOrEqualTo(64);
        Guid.TryParse(returned, out _).Should().BeTrue();
    }

    // ── correlationId in response bodies ─────────────────────────────────────

    [Fact]
    public async Task Success_Response_Should_Include_CorrelationId_In_Body()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "obs-success@example.com",
            name = "Test User",
            password = "Secret1@pass"
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Error_Response_Should_Include_CorrelationId_In_Body()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sessions", new
        {
            email = "nobody-obs@example.com",
            password = "wrong"
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CorrelationId_In_Body_Should_Match_Response_Header()
    {
        var id = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                email = "obs-match@example.com",
                name = "Test",
                password = "Secret1@pass"
            })
        };
        request.Headers.Add("X-Correlation-Id", id);

        var response = await _client.SendAsync(request);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("correlationId").GetString().Should().Be(id);
        response.Headers.GetValues("X-Correlation-Id").First().Should().Be(id);
    }

    // ── Global Exception Handler ──────────────────────────────────────────────

    [Fact]
    public async Task Exception_Handler_Should_Return_500_With_Safe_Message()
    {
        var response = await _client.GetAsync("/test/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("internal_error");
        body.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Exception_Handler_Should_Not_Expose_Internal_Details()
    {
        var response = await _client.GetAsync("/test/throw");
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain("InvalidOperationException");
        raw.Should().NotContain("StackTrace");
        raw.Should().NotContain("Test exception from M7");
    }

    // ── Health Endpoint ───────────────────────────────────────────────────────

    [Fact]
    public async Task Health_Should_Return_200_With_Healthy_Status()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("healthy");
        body.GetProperty("database").GetString().Should().Be("connected");
    }

    [Fact]
    public async Task Health_Should_Not_Require_Authentication()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }
}
