using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Helpdesk.API.Audit;
using Helpdesk.API.Persistence;
using Helpdesk.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Integration.Observability;

[Collection("auth-integration")]
public sealed class AuditTrailTests(HelpdeskWebAppFactory factory)
    : IClassFixture<HelpdeskWebAppFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClientWithCookies();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Set<AuditEvent>().ExecuteDeleteAsync();
    }

    private async Task<List<AuditEvent>> GetAuditEventsAsync(string eventType)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<AuditEvent>().Where(e => e.EventType == eventType).ToListAsync();
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_Should_Create_UserRegistered_Audit_Event()
    {
        var email = $"audit-reg-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, name = "Audit", password = "Secret1@pass" });

        var events = await GetAuditEventsAsync("UserRegistered");
        events.Should().Contain(e => e.AggregateType == "Identity");
    }

    // ── Email Verification ────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_Should_Create_EmailVerified_Audit_Event()
    {
        var email = $"audit-verify-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, name = "Audit", password = "Secret1@pass" });

        var token = factory.EmailService.GetVerificationToken(email)!;
        await _client.PatchAsJsonAsync("/api/auth/email-verifications", new { token });

        var events = await GetAuditEventsAsync("EmailVerified");
        events.Should().NotBeEmpty();
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_Should_Create_UserLoggedIn_Audit_Event()
    {
        var (email, password, _) = await factory.SeedUserAsync(
            Helpdesk.Modules.Identity.Domain.Enums.UserRole.SupportAgent);

        await _client.PostAsJsonAsync("/api/auth/sessions", new { email, password });

        var events = await GetAuditEventsAsync("UserLoggedIn");
        events.Should().NotBeEmpty();
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_Should_Create_UserLoggedOut_Audit_Event()
    {
        var (email, password, _) = await factory.SeedUserAsync(
            Helpdesk.Modules.Identity.Domain.Enums.UserRole.SupportAgent);
        await _client.PostAsJsonAsync("/api/auth/sessions", new { email, password });

        await _client.DeleteAsync("/api/auth/sessions/current");

        var events = await GetAuditEventsAsync("UserLoggedOut");
        events.Should().NotBeEmpty();
    }

    // ── Password Reset ────────────────────────────────────────────────────────

    [Fact]
    public async Task RequestPasswordReset_Should_Create_PasswordResetRequested_Audit_Event()
    {
        var (email, _, _) = await factory.SeedUserAsync(
            Helpdesk.Modules.Identity.Domain.Enums.UserRole.SupportAgent);

        await _client.PostAsJsonAsync("/api/auth/password-resets", new { email });

        var events = await GetAuditEventsAsync("PasswordResetRequested");
        events.Should().NotBeEmpty();
    }

    // ── Ticket Created ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTicket_Should_Create_TicketCreated_Audit_Event()
    {
        var (email, password, customerId) = await factory.SeedUserAsync(
            Helpdesk.Modules.Identity.Domain.Enums.UserRole.Customer);
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/sessions", new { email, password });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var accessToken = loginBody.GetProperty("data").GetProperty("accessToken").GetString()!;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
        {
            Content = JsonContent.Create(new
            {
                title = "Audit Test Ticket",
                description = "Testing audit trail for ticket creation",
                priority = "Medium",
                category = "Support"
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        await _client.SendAsync(request);

        var events = await GetAuditEventsAsync("TicketCreated");
        events.Should().Contain(e => e.AggregateType == "Ticket");
    }

    // ── correlationId ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTicket_Should_Persist_CorrelationId_In_Audit_Event()
    {
        var (email, password, _) = await factory.SeedUserAsync(
            Helpdesk.Modules.Identity.Domain.Enums.UserRole.Customer);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/sessions", new { email, password });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = loginBody.GetProperty("data").GetProperty("accessToken").GetString()!;

        var correlationId = $"test-{Guid.NewGuid():N}";

        using var client = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Correlation test",
            description = "Testing correlationId persistence",
            priority = "Low",
            category = "Support"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = Guid.Parse(responseBody.GetProperty("data").GetProperty("ticketId").GetString()!);

        var events = await GetAuditEventsAsync("TicketCreated");
        var auditEvent = events.Single(e => e.AggregateId == ticketId);
        auditEvent.CorrelationId.Should().Be(correlationId);
    }

    // ── Ticket Assigned ───────────────────────────────────────────────────────

    [Fact]
    public async Task AssignTicket_Should_Create_TicketAssigned_Audit_Event()
    {
        var (agentEmail, agentPassword, agentId) = await factory.SeedUserAsync(
            Helpdesk.Modules.Identity.Domain.Enums.UserRole.SupportAgent);
        var (custEmail, custPassword, _) = await factory.SeedUserAsync(
            Helpdesk.Modules.Identity.Domain.Enums.UserRole.Customer);

        var agentLogin = await _client.PostAsJsonAsync("/api/auth/sessions", new { email = agentEmail, password = agentPassword });
        var agentBody = await agentLogin.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var agentToken = agentBody.GetProperty("data").GetProperty("accessToken").GetString()!;

        var custLogin = await _client.PostAsJsonAsync("/api/auth/sessions", new { email = custEmail, password = custPassword });
        var custBody = await custLogin.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var custToken = custBody.GetProperty("data").GetProperty("accessToken").GetString()!;

        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/tickets")
        {
            Content = JsonContent.Create(new { title = "Assign Test", description = "Testing assign audit", priority = "Medium", category = "Support" })
        };
        createReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", custToken);
        var createResp = await _client.SendAsync(createReq);
        var createBody = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var ticketId = createBody.GetProperty("data").GetProperty("ticketId").GetString()!;

        var assignReq = new HttpRequestMessage(HttpMethod.Post, $"/api/tickets/{ticketId}/assign")
        {
            Content = JsonContent.Create(new { agentId })
        };
        assignReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", agentToken);
        await _client.SendAsync(assignReq);

        var events = await GetAuditEventsAsync("TicketAssigned");
        events.Should().Contain(e => e.AggregateType == "Ticket");
    }
}
