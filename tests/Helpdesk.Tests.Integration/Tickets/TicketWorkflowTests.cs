using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Tests.Integration.Infrastructure;

namespace Helpdesk.Tests.Integration.Tickets;

[Collection("auth-integration")]
public sealed class TicketWorkflowTests(HelpdeskWebAppFactory factory)
    : IClassFixture<HelpdeskWebAppFactory>
{
    // ── Helpers ──────────────────────────────────────────────────────────────

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

    private async Task<Guid> CreateTicketAsync(string customerToken)
    {
        using var client = AuthClient(customerToken);
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Test ticket",
            description = "Test description",
            priority = "Low",
            category = "Support"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("data").GetProperty("ticketId").GetString()!);
    }

    private async Task<Guid> CreateAndAssignTicketAsync(string customerToken, string agentToken)
    {
        var ticketId = await CreateTicketAsync(customerToken);
        using var client = AuthClient(agentToken);
        var r = await client.PostAsync($"/api/tickets/{ticketId}/assign", null);
        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return ticketId;
    }

    // ── POST /api/tickets ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTicket_Should_Return_201_With_TicketId()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.Customer);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Printer not working",
            description = "Office printer gives error 503",
            priority = "High",
            category = "Bug"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("ticketId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateTicket_Should_Return_401_Without_Token()
    {
        using var client = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };
        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Test",
            description = "Test"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTicket_Should_Return_403_For_Agent()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Test",
            description = "Test"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTicket_Should_Return_400_When_Title_Is_Empty()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.Customer);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "",
            description = "Some description"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTicket_Should_Return_400_When_Priority_Is_Invalid()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.Customer);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync("/api/tickets",
            new { title = "Test", description = "Test", priority = "InvalidPriority" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/tickets/{id}/assign ────────────────────────────────────────

    [Fact]
    public async Task AssignTicket_Should_Return_204_When_Agent_Assumes_Open_Ticket()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsync($"/api/tickets/{ticketId}/assign", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignTicket_Should_Return_409_When_Ticket_Is_Already_InProgress()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsync($"/api/tickets/{ticketId}/assign", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AssignTicket_Should_Return_409_When_Ticket_Is_In_Final_State()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentToken);

        using var client = AuthClient(agentToken);
        await client.PostAsJsonAsync($"/api/tickets/{ticketId}/resolve",
            new { description = "Resolved the issue." });

        var response = await client.PostAsync($"/api/tickets/{ticketId}/assign", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AssignTicket_Should_Return_404_When_Ticket_Not_Found()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        using var client = AuthClient(token);

        var response = await client.PostAsync($"/api/tickets/{Guid.NewGuid()}/assign", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignTicket_Should_Return_403_For_Customer()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsync($"/api/tickets/{ticketId}/assign", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST /api/tickets/{id}/resolve ───────────────────────────────────────

    [Fact]
    public async Task ResolveTicket_Should_Return_204_When_Assignee_Resolves()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/resolve",
            new { description = "Fixed by restarting the service." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResolveTicket_Should_Return_403_When_Not_Assignee()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentAToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var (agentBToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentAToken);

        using var client = AuthClient(agentBToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/resolve",
            new { description = "I'll resolve this too." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResolveTicket_Should_Return_400_When_Description_Is_Empty()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/resolve",
            new { description = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResolveTicket_Should_Return_404_When_Ticket_Not_Found()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/resolve",
            new { description = "Resolved." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/tickets/{id}/cancel ────────────────────────────────────────

    [Fact]
    public async Task CancelTicket_Should_Return_204_When_Customer_Cancels_Own_Ticket()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/cancel",
            new { reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CancelTicket_Should_Return_204_When_Manager_Cancels_With_Reason()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (managerToken, _) = await SeedAndLoginAsync(UserRole.Manager);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(managerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/cancel",
            new { reason = "Duplicate of ticket #42." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CancelTicket_Should_Return_400_When_Manager_Cancels_Without_Reason()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (managerToken, _) = await SeedAndLoginAsync(UserRole.Manager);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(managerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/cancel",
            new { reason = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelTicket_Should_Return_409_When_Ticket_Is_In_Final_State()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentToken);

        using var agentClient = AuthClient(agentToken);
        await agentClient.PostAsJsonAsync($"/api/tickets/{ticketId}/resolve",
            new { description = "Done." });

        using var customerClient = AuthClient(customerToken);
        var response = await customerClient.PostAsJsonAsync($"/api/tickets/{ticketId}/cancel",
            new { reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelTicket_Should_Return_404_When_Ticket_Not_Found()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.Customer);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/cancel",
            new { reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/tickets/{id}/transfer ──────────────────────────────────────

    [Fact]
    public async Task TransferTicket_Should_Return_204_When_Assignee_Transfers()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentAToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var (_, agentBId) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentAToken);

        using var client = AuthClient(agentAToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/transfer",
            new { newAssigneeId = agentBId, reason = "Reassigning to specialist." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferTicket_Should_Return_409_When_Ticket_Not_InProgress()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/transfer",
            new { newAssigneeId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TransferTicket_Should_Return_403_When_Not_Assignee()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentAToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var (agentBToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentAToken);

        using var client = AuthClient(agentBToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/transfer",
            new { newAssigneeId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TransferTicket_Should_Return_404_When_Ticket_Not_Found()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/transfer",
            new { newAssigneeId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH /api/tickets/{id}/priority ─────────────────────────────────────

    [Fact]
    public async Task ChangePriority_Should_Return_204_When_Assignee_Changes_Priority()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentToken);

        using var client = AuthClient(agentToken);
        var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/priority",
            new { priority = "High" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangePriority_Should_Return_409_When_Max_Changes_Reached()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentToken);

        using var client = AuthClient(agentToken);
        await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/priority", new { priority = "High" });
        await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/priority", new { priority = "Medium" });
        await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/priority", new { priority = "Low" });

        var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/priority",
            new { priority = "High" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangePriority_Should_Return_403_When_Not_Assignee()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentAToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var (agentBToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentAToken);

        using var client = AuthClient(agentBToken);
        var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/priority",
            new { priority = "High" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangePriority_Should_Return_404_When_Ticket_Not_Found()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        using var client = AuthClient(token);

        var response = await client.PatchAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/priority",
            new { priority = "High" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/tickets + GET /api/tickets/{id} ──────────────────────────────

    [Fact]
    public async Task GetTickets_Should_Return_Only_Customer_Own_Tickets()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);

        await CreateTicketAsync(customerAToken);
        await CreateTicketAsync(customerAToken);
        await CreateTicketAsync(customerBToken);

        using var client = AuthClient(customerAToken);
        var response = await client.GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tickets = body.GetProperty("data").EnumerateArray().ToList();
        tickets.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTickets_Should_Return_All_Tickets_For_Agent()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        await CreateTicketAsync(customerAToken);
        await CreateTicketAsync(customerBToken);

        using var client = AuthClient(agentToken);
        var response = await client.GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tickets = body.GetProperty("data").EnumerateArray().ToList();
        tickets.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetTicket_Should_Return_200_With_Ticket_Details()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.GetAsync($"/api/tickets/{ticketId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        data.GetProperty("id").GetString().Should().Be(ticketId.ToString());
        data.GetProperty("title").GetString().Should().Be("Test ticket");
        data.GetProperty("status").GetString().Should().Be("Open");
    }

    [Fact]
    public async Task GetTicket_Should_Return_404_When_Not_Found()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.Customer);
        using var client = AuthClient(token);

        var response = await client.GetAsync($"/api/tickets/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTickets_Should_Return_401_Without_Token()
    {
        using var client = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };
        var response = await client.GetAsync("/api/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTicket_Should_Return_404_When_Customer_Accesses_Another_Customers_Ticket()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerAToken);

        using var client = AuthClient(customerBToken);
        var response = await client.GetAsync($"/api/tickets/{ticketId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTicket_Should_Return_400_When_Title_Exceeds_200_Characters()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.Customer);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = new string('A', 201),
            description = "Valid description",
            priority = "Low",
            category = "Support"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTicket_Should_Return_400_When_Description_Exceeds_2000_Characters()
    {
        var (token, _) = await SeedAndLoginAsync(UserRole.Customer);
        using var client = AuthClient(token);

        var response = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Valid title",
            description = new string('A', 2001),
            priority = "Low",
            category = "Support"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/tickets/{id}/resolve — missing paths ───────────────────────

    [Fact]
    public async Task ResolveTicket_Should_Return_409_When_Ticket_Is_Open()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/resolve",
            new { description = "Trying to resolve an Open ticket." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ResolveTicket_Should_Return_409_When_Ticket_Is_Already_Resolved()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateAndAssignTicketAsync(customerToken, agentToken);

        using var client = AuthClient(agentToken);
        await client.PostAsJsonAsync($"/api/tickets/{ticketId}/resolve",
            new { description = "First resolution." });

        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/resolve",
            new { description = "Trying to resolve again." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── POST /api/tickets/{id}/cancel — missing paths ────────────────────────

    [Fact]
    public async Task CancelTicket_Should_Return_403_When_Customer_Cancels_Another_Customers_Ticket()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerAToken);

        using var client = AuthClient(customerBToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/cancel",
            new { reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelTicket_Should_Return_403_When_Agent_Cancels_Ticket()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/cancel",
            new { reason = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PATCH /api/tickets/{id}/priority — missing path ──────────────────────

    [Fact]
    public async Task ChangePriority_Should_Return_409_When_Ticket_Is_Open()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(agentToken);
        var response = await client.PatchAsJsonAsync($"/api/tickets/{ticketId}/priority",
            new { priority = "High" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
