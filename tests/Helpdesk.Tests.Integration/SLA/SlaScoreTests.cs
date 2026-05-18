using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Helpdesk.API.Persistence;
using Helpdesk.API.SLA;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Integration.SLA;

[Collection("auth-integration")]
public sealed class SlaScoreTests(HelpdeskWebAppFactory factory)
    : IClassFixture<HelpdeskWebAppFactory>, IAsyncLifetime
{
    // ── Per-test setup ───────────────────────────────────────────────────────

    // Truncate all tables between tests to prevent state from previous tests
    // from affecting manager load-selection and score calculations.
    public async Task InitializeAsync()
    {
        ResetClock();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ticket_attachments");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ticket_comments");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM tickets");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM sla_monthly_scores");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM user_sessions");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM email_verification_tokens");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM password_reset_tokens");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM users");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Reset clock to real time so JWT tokens are valid (nbf check passes).
    // Advance the clock AFTER obtaining all tokens.
    private void ResetClock() => factory.TimeProvider.SetUtcNow(DateTime.UtcNow);

    private HttpClient AuthClient(string token)
    {
        var client = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(string token, Guid userId)> SeedAndLoginAsync(UserRole role)
    {
        var (email, password, userId) = await factory.SeedUserAsync(role);
        using var raw = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };
        var response = await raw.PostAsJsonAsync("/api/auth/sessions", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("data").GetProperty("accessToken").GetString()!;
        return (token, userId);
    }

    private async Task<Guid> CreateTicketAsync(string customerToken, string priority = "Low")
    {
        using var client = AuthClient(customerToken);
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "SLA test ticket",
            description = "Testing SLA engine",
            priority,
            category = "Support"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("data").GetProperty("ticketId").GetString()!);
    }

    private async Task AssignTicketAsync(string agentToken, Guid ticketId)
    {
        using var client = AuthClient(agentToken);
        var response = await client.PostAsync($"/api/tickets/{ticketId}/assignments", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task ResolveTicketAsync(string agentToken, Guid ticketId)
    {
        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/resolution",
            new { description = "Resolved by agent." });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task RunProcessorAsync()
    {
        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<SlaBreachProcessor>();
        await processor.ProcessAsync();
    }

    private async Task<JsonElement> GetScoresDataAsync(string agentToken)
    {
        using var client = AuthClient(agentToken);
        var response = await client.GetAsync("/api/sla/scores");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("data");
    }

    private async Task<int> GetCurrentScoreAsync(string agentToken)
    {
        var data = await GetScoresDataAsync(agentToken);
        return data.GetProperty("currentMonth").GetProperty("score").GetInt32();
    }

    private async Task<int> GetTicketsWithinSlaAsync(string agentToken)
    {
        var data = await GetScoresDataAsync(agentToken);
        return data.GetProperty("currentMonth").GetProperty("ticketsWithinSla").GetInt32();
    }

    private async Task<int> GetTicketsBreachedAsync(string agentToken)
    {
        var data = await GetScoresDataAsync(agentToken);
        return data.GetProperty("currentMonth").GetProperty("ticketsBreached").GetInt32();
    }

    // ── GET /api/sla/scores — access control ─────────────────────────────────

    [Fact]
    public async Task GetScores_Should_Return_401_When_Not_Authenticated()
    {
        using var client = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };
        var response = await client.GetAsync("/api/sla/scores");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetScores_Should_Return_403_For_Customer()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        using var client = AuthClient(customerToken);
        var response = await client.GetAsync("/api/sla/scores");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetScores_Should_Return_200_For_Agent_With_Score_Fields()
    {
        ResetClock();
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var data = await GetScoresDataAsync(agentToken);

        data.GetProperty("currentMonth").TryGetProperty("score", out _).Should().BeTrue();
        data.GetProperty("currentMonth").TryGetProperty("ticketsWithinSla", out _).Should().BeTrue();
        data.GetProperty("currentMonth").TryGetProperty("ticketsBreached", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetScores_Should_Return_200_For_Manager()
    {
        ResetClock();
        var (managerToken, _) = await SeedAndLoginAsync(UserRole.Manager);
        using var client = AuthClient(managerToken);
        var response = await client.GetAsync("/api/sla/scores");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Score: resolution within SLA ─────────────────────────────────────────

    [Fact]
    public async Task Score_Should_Increase_By_100_When_Ticket_Resolved_Within_SLA()
    {
        // Get all tokens FIRST while clock is at real time so JWT nbf check passes.
        // Advance clock only after tokens are obtained.
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        var scoreBefore = await GetCurrentScoreAsync(agentToken);
        var withinBefore = await GetTicketsWithinSlaAsync(agentToken);

        // Create Low-priority ticket (SLA = 4h from now)
        var ticketId = await CreateTicketAsync(customerToken, "Low");
        await AssignTicketAsync(agentToken, ticketId);

        // Advance 1h — within SLA window — then resolve
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        await ResolveTicketAsync(agentToken, ticketId);

        await RunProcessorAsync();

        var scoreAfter = await GetCurrentScoreAsync(agentToken);
        var withinAfter = await GetTicketsWithinSlaAsync(agentToken);

        (scoreAfter - scoreBefore).Should().Be(100);
        (withinAfter - withinBefore).Should().Be(1);
    }

    [Fact]
    public async Task Score_Should_Deduct_10_Per_Overdue_Hour_When_Resolved_After_SLA()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        var scoreBefore = await GetCurrentScoreAsync(agentToken);
        var breachedBefore = await GetTicketsBreachedAsync(agentToken);

        // High priority = 1h SLA
        var ticketId = await CreateTicketAsync(customerToken, "High");
        await AssignTicketAsync(agentToken, ticketId);

        // Resolve at T+4h — 3h overdue → delta = -30
        factory.TimeProvider.Advance(TimeSpan.FromHours(4));
        await ResolveTicketAsync(agentToken, ticketId);

        await RunProcessorAsync();

        var scoreAfter = await GetCurrentScoreAsync(agentToken);
        var breachedAfter = await GetTicketsBreachedAsync(agentToken);

        (scoreAfter - scoreBefore).Should().Be(-30);
        (breachedAfter - breachedBefore).Should().Be(1);
    }

    [Fact]
    public async Task Score_Should_Not_Go_Below_Minus_100_Floor()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        // Drive score to exactly -100 first (if not already there)
        // Then verify it can't go lower — resolve 15h overdue (High = 1h SLA, -140 without floor)
        var ticketId = await CreateTicketAsync(customerToken, "High");
        await AssignTicketAsync(agentToken, ticketId);

        factory.TimeProvider.Advance(TimeSpan.FromHours(16));
        await ResolveTicketAsync(agentToken, ticketId);

        await RunProcessorAsync();

        var scoreAfter = await GetCurrentScoreAsync(agentToken);
        scoreAfter.Should().BeGreaterThanOrEqualTo(-100);
    }

    // ── Score: manager cancellation counts ────────────────────────────────────

    [Fact]
    public async Task Score_Should_Increase_By_100_When_Manager_Cancels_Within_SLA()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (managerToken, _) = await SeedAndLoginAsync(UserRole.Manager);

        var scoreBefore = await GetCurrentScoreAsync(managerToken);

        // Low priority = 4h SLA; cancel at T+1h → within SLA
        var ticketId = await CreateTicketAsync(customerToken, "Low");
        factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using var client = AuthClient(managerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/cancellation",
            new { reason = "Duplicate request, closing." });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await RunProcessorAsync();

        var scoreAfter = await GetCurrentScoreAsync(managerToken);
        (scoreAfter - scoreBefore).Should().Be(100);
    }

    // ── Score: customer cancellation excluded ─────────────────────────────────

    [Fact]
    public async Task Score_Should_Not_Change_When_Customer_Cancels_Ticket()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        var scoreBefore = await GetCurrentScoreAsync(agentToken);

        var ticketId = await CreateTicketAsync(customerToken, "Low");
        factory.TimeProvider.Advance(TimeSpan.FromMinutes(30));

        using var client = AuthClient(customerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/cancellation",
            new { reason = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await RunProcessorAsync();

        var scoreAfter = await GetCurrentScoreAsync(agentToken);
        (scoreAfter - scoreBefore).Should().Be(0);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Score_Should_Not_Be_Applied_Twice_When_Processor_Runs_Twice()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        var scoreBefore = await GetCurrentScoreAsync(agentToken);

        var ticketId = await CreateTicketAsync(customerToken, "Low");
        await AssignTicketAsync(agentToken, ticketId);

        factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        await ResolveTicketAsync(agentToken, ticketId);

        // Run processor twice — score must be applied only once
        await RunProcessorAsync();
        await RunProcessorAsync();

        var scoreAfter = await GetCurrentScoreAsync(agentToken);
        (scoreAfter - scoreBefore).Should().Be(100);
    }

    // ── Breach detection ──────────────────────────────────────────────────────

    [Fact]
    public async Task Processor_Should_Mark_Ticket_As_Breached_After_SLA_Deadline()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        // High priority = 1h SLA; leave unassigned
        var ticketId = await CreateTicketAsync(customerToken, "High");

        factory.TimeProvider.Advance(TimeSpan.FromHours(2));
        await RunProcessorAsync();

        // Ticket should still exist (not resolved/cancelled)
        using var client = AuthClient(agentToken);
        var response = await client.GetAsync($"/api/tickets/{ticketId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Unassigned penalty ────────────────────────────────────────────────────

    [Fact]
    public async Task Score_Should_Deduct_5_Per_2h_For_Unassigned_Breached_Ticket()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        var scoreBefore = await GetCurrentScoreAsync(agentToken);

        // High priority = 1h SLA, left unassigned → breach at T+1h
        // Advance 6h total → 5h past SLA deadline: floor(5/2) = 2 complete 2h windows → -10 penalty
        var ticketId = await CreateTicketAsync(customerToken, "High");

        factory.TimeProvider.Advance(TimeSpan.FromHours(6));
        await RunProcessorAsync();

        var scoreAfter = await GetCurrentScoreAsync(agentToken);
        // 2 windows of 2h each (5h since SlaDueAt / 2h per window, truncated) → -10 deducted
        (scoreAfter - scoreBefore).Should().Be(-10);
    }

    // ── Auto-assign fallback ──────────────────────────────────────────────────

    [Fact]
    public async Task Processor_Should_AutoAssign_Unassigned_Ticket_To_Manager_After_SLA_Breach()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (managerToken, managerId) = await SeedAndLoginAsync(UserRole.Manager);

        // High priority = 1h SLA, unassigned
        var ticketId = await CreateTicketAsync(customerToken, "High");

        factory.TimeProvider.Advance(TimeSpan.FromHours(2));
        await RunProcessorAsync();

        using var client = AuthClient(managerToken);
        var response = await client.GetAsync($"/api/tickets/{ticketId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var assigneeId = body.GetProperty("data").GetProperty("assigneeId").GetString();
        assigneeId.Should().Be(managerId.ToString());
    }

    [Fact]
    public async Task AutoAssign_Should_Pick_Manager_With_Lowest_Active_Ticket_Count()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (managerAToken, managerAId) = await SeedAndLoginAsync(UserRole.Manager);
        var (managerBToken, managerBId) = await SeedAndLoginAsync(UserRole.Manager);

        // Give manager A an active (In Progress) ticket
        var busyTicket = await CreateTicketAsync(customerToken, "Low");
        await AssignTicketAsync(managerAToken, busyTicket);

        // Unassigned ticket that will breach SLA (High = 1h)
        var targetTicket = await CreateTicketAsync(customerToken, "High");

        factory.TimeProvider.Advance(TimeSpan.FromHours(2));
        await RunProcessorAsync();

        // Manager B has 0 active tickets → should receive the auto-assign
        using var client = AuthClient(managerBToken);
        var response = await client.GetAsync($"/api/tickets/{targetTicket}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var assigneeId = body.GetProperty("data").GetProperty("assigneeId").GetString();
        assigneeId.Should().Be(managerBId.ToString());
    }

    // ── Auto-cancel ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Processor_Should_AutoCancel_Ticket_10h_After_AutoAssign()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (managerToken, _) = await SeedAndLoginAsync(UserRole.Manager);

        // High = 1h SLA, unassigned → breach → auto-assign
        var ticketId = await CreateTicketAsync(customerToken, "High");

        factory.TimeProvider.Advance(TimeSpan.FromHours(2));
        await RunProcessorAsync(); // breach + auto-assign

        // Advance 10h more → auto-cancel
        factory.TimeProvider.Advance(TimeSpan.FromHours(10));
        await RunProcessorAsync();

        using var client = AuthClient(managerToken);
        var response = await client.GetAsync($"/api/tickets/{ticketId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var status = body.GetProperty("data").GetProperty("status").GetString();
        status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Processor_Should_Not_AutoCancel_Ticket_Resolved_Before_10h()
    {
        ResetClock();
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (managerToken, managerId) = await SeedAndLoginAsync(UserRole.Manager);

        var ticketId = await CreateTicketAsync(customerToken, "High");

        // Breach + auto-assign
        factory.TimeProvider.Advance(TimeSpan.FromHours(2));
        await RunProcessorAsync();

        // Manager resolves the ticket before the 10h auto-cancel window
        await ResolveTicketAsync(managerToken, ticketId);

        // Advance another 10h
        factory.TimeProvider.Advance(TimeSpan.FromHours(10));
        await RunProcessorAsync();

        using var client = AuthClient(managerToken);
        var response = await client.GetAsync($"/api/tickets/{ticketId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var status = body.GetProperty("data").GetProperty("status").GetString();
        status.Should().Be("Resolved");
    }
}
