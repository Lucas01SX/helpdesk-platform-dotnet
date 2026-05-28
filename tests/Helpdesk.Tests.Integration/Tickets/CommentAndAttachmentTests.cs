using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Helpdesk.API.Persistence;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Integration.Tickets;

[Collection("auth-integration")]
public sealed class CommentAndAttachmentTests(HelpdeskWebAppFactory factory)
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

    private static MultipartFormDataContent BuildFileContent(
        string content = "file content", string fileName = "test.txt", string mimeType = "text/plain")
    {
        var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        bytes.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(bytes, "file", fileName);
        return form;
    }

    // ── POST /api/tickets/{id}/comments ──────────────────────────────────────

    [Fact]
    public async Task AddComment_Should_Return_201_When_Customer_Adds_Public_Comment_To_Own_Ticket()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "I still have this issue.", visibility = "Public" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("commentId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AddComment_Should_Return_201_When_Agent_Adds_Public_Comment()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "Looking into it now.", visibility = "Public" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddComment_Should_Return_201_When_Agent_Adds_Internal_Comment()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "Internal note: escalate to tier 2.", visibility = "Internal" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddComment_Should_Return_403_When_Customer_Requests_Internal_Visibility()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "Sneaky internal.", visibility = "Internal" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddComment_Should_Return_404_When_Customer_Comments_On_Another_Customers_Ticket()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerAToken);

        using var client = AuthClient(customerBToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "Not my ticket.", visibility = "Public" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddComment_Should_Return_400_When_Content_Exceeds_4000_Characters()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = new string('x', 4001), visibility = "Public" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddComment_Should_Return_400_When_Content_Is_Empty()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "   ", visibility = "Public" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddComment_Should_Return_404_When_Ticket_Not_Found()
    {
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/comments",
            new { content = "Hello.", visibility = "Public" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddComment_Should_Return_401_Without_Token()
    {
        using var client = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };
        var response = await client.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/comments",
            new { content = "Hello.", visibility = "Public" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/tickets/{id}/comments ───────────────────────────────────────

    [Fact]
    public async Task ListComments_Should_Return_Only_Public_For_Customer()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var agentClient = AuthClient(agentToken);
        await agentClient.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "Public note.", visibility = "Public" });
        await agentClient.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "Internal note.", visibility = "Internal" });

        using var customerClient = AuthClient(customerToken);
        var response = await customerClient.GetAsync($"/api/tickets/{ticketId}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var comments = body.GetProperty("data").EnumerateArray().ToList();
        comments.Should().HaveCount(1);
        comments[0].GetProperty("visibility").GetString().Should().Be("Public");
    }

    [Fact]
    public async Task ListComments_Should_Return_All_For_Agent()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var agentClient = AuthClient(agentToken);
        await agentClient.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "Public note.", visibility = "Public" });
        await agentClient.PostAsJsonAsync($"/api/tickets/{ticketId}/comments",
            new { content = "Internal note.", visibility = "Internal" });

        var response = await agentClient.GetAsync($"/api/tickets/{ticketId}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var comments = body.GetProperty("data").EnumerateArray().ToList();
        comments.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListComments_Should_Return_404_When_Customer_Accesses_Another_Customers_Ticket()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerAToken);

        using var client = AuthClient(customerBToken);
        var response = await client.GetAsync($"/api/tickets/{ticketId}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListComments_Should_Return_404_When_Ticket_Not_Found()
    {
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        using var client = AuthClient(agentToken);
        var response = await client.GetAsync($"/api/tickets/{Guid.NewGuid()}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/tickets/{id}/attachments ───────────────────────────────────

    [Fact]
    public async Task UploadAttachment_Should_Return_201_When_Customer_Uploads_To_Own_Ticket()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("attachmentId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_201_When_Agent_Uploads_Internal()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Internal",
            BuildFileContent("internal log data", "debug.log", "text/plain"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_403_When_Customer_Requests_Internal_Visibility()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Internal",
            BuildFileContent());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_404_When_Customer_Uploads_To_Another_Ticket()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerAToken);

        using var client = AuthClient(customerBToken);
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_400_When_No_File()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            new MultipartFormDataContent());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_400_When_File_Too_Large()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var form = new MultipartFormDataContent();
        var largeFile = new ByteArrayContent(new byte[11 * 1024 * 1024]);
        largeFile.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(largeFile, "file", "large.bin");

        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_404_When_Ticket_Not_Found()
    {
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        using var client = AuthClient(agentToken);
        var response = await client.PostAsync(
            $"/api/tickets/{Guid.NewGuid()}/attachments?visibility=Public",
            BuildFileContent());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/tickets/{id}/attachments ────────────────────────────────────

    [Fact]
    public async Task ListAttachments_Should_Return_Only_Public_For_Customer()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var agentClient = AuthClient(agentToken);
        await agentClient.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("public", "public.txt"));
        await agentClient.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Internal",
            BuildFileContent("internal", "internal.txt"));

        using var customerClient = AuthClient(customerToken);
        var response = await customerClient.GetAsync($"/api/tickets/{ticketId}/attachments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var attachments = body.GetProperty("data").EnumerateArray().ToList();
        attachments.Should().HaveCount(1);
        attachments[0].GetProperty("visibility").GetString().Should().Be("Public");
    }

    [Fact]
    public async Task ListAttachments_Should_Return_All_For_Agent()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var agentClient = AuthClient(agentToken);
        await agentClient.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("public", "pub.txt"));
        await agentClient.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Internal",
            BuildFileContent("internal", "int.txt"));

        var response = await agentClient.GetAsync($"/api/tickets/{ticketId}/attachments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var attachments = body.GetProperty("data").EnumerateArray().ToList();
        attachments.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAttachments_Should_Return_404_When_Customer_Accesses_Another_Tickets_Attachments()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerAToken);

        using var client = AuthClient(customerBToken);
        var response = await client.GetAsync($"/api/tickets/{ticketId}/attachments");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/tickets/{id}/attachments/{attachmentId} ─────────────────────

    [Fact]
    public async Task DownloadAttachment_Should_Return_File_Content()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var uploadResp = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("hello from file", "hello.txt"));
        var uploadBody = await uploadResp.Content.ReadFromJsonAsync<JsonElement>();
        var attachmentId = uploadBody.GetProperty("data").GetProperty("attachmentId").GetString()!;

        var downloadResp = await client.GetAsync(
            $"/api/tickets/{ticketId}/attachments/{attachmentId}");

        downloadResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await downloadResp.Content.ReadAsStringAsync();
        content.Should().Be("hello from file");
    }

    [Fact]
    public async Task DownloadAttachment_Should_Return_403_When_Customer_Downloads_Internal()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);
        var ticketId = await CreateTicketAsync(customerToken);

        using var agentClient = AuthClient(agentToken);
        var uploadResp = await agentClient.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Internal",
            BuildFileContent("secret", "secret.txt"));
        var uploadBody = await uploadResp.Content.ReadFromJsonAsync<JsonElement>();
        var attachmentId = uploadBody.GetProperty("data").GetProperty("attachmentId").GetString()!;

        using var customerClient = AuthClient(customerToken);
        var downloadResp = await customerClient.GetAsync(
            $"/api/tickets/{ticketId}/attachments/{attachmentId}");

        downloadResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DownloadAttachment_Should_Return_404_When_Not_Found()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.GetAsync(
            $"/api/tickets/{ticketId}/attachments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/tickets/{id}/attachments — file type hardening ─────────────

    [Fact]
    public async Task UploadAttachment_Should_Return_400_When_Mime_Type_Is_Blocked()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        // Valid extension, blocked MIME — MIME whitelist must reject it
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("content", "report.txt", "application/x-msdownload"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("attachment.file_type_not_allowed");
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_400_When_Extension_Is_Blocked()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        // Blocked extension — extension whitelist must reject it
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("MZ", "malware.exe", "application/octet-stream"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("attachment.file_type_not_allowed");
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_400_When_Blocked_Extension_Disguised_As_Valid_Mime()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        // .exe disguised as image/jpeg — extension check must reject regardless of MIME
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("MZ", "malware.exe", "image/jpeg"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("attachment.file_type_not_allowed");
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_201_For_Pdf_With_Correct_Mime()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("%PDF-1.4", "report.pdf", "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UploadAttachment_Should_Return_201_For_Jpeg_With_Correct_Mime()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("jpeg data", "photo.jpg", "image/jpeg"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ListAttachments_Should_Return_404_When_Ticket_Not_Found()
    {
        var (agentToken, _) = await SeedAndLoginAsync(UserRole.SupportAgent);

        using var client = AuthClient(agentToken);
        var response = await client.GetAsync($"/api/tickets/{Guid.NewGuid()}/attachments");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/tickets/{id}/attachments/{attachmentId} — IDOR ─────────────

    [Fact]
    public async Task DownloadAttachment_Should_Return_404_When_Customer_Downloads_Another_Customers_Attachment()
    {
        var (customerAToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var (customerBToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerAToken);

        using var agentClientA = AuthClient(customerAToken);
        var uploadResp = await agentClientA.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("public data", "public.txt"));
        var uploadBody = await uploadResp.Content.ReadFromJsonAsync<JsonElement>();
        var attachmentId = uploadBody.GetProperty("data").GetProperty("attachmentId").GetString()!;

        using var clientB = AuthClient(customerBToken);
        var response = await clientB.GetAsync(
            $"/api/tickets/{ticketId}/attachments/{attachmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadAttachment_Should_Store_File_With_Internal_Name_Only()
    {
        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = AuthClient(customerToken);
        const string originalFileName = "my-secret-report.pdf";
        var response = await client.PostAsync(
            $"/api/tickets/{ticketId}/attachments?visibility=Public",
            BuildFileContent("%PDF-1.4", originalFileName, "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var attachmentId = Guid.Parse(
            body.GetProperty("data").GetProperty("attachmentId").GetString()!);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attachment = await db.Set<TicketAttachment>().FindAsync(attachmentId);

        attachment!.FileName.Should().Be(originalFileName);
        attachment.StoragePath.Should().NotContain(originalFileName);
        Path.GetFileName(attachment.StoragePath).Should().MatchRegex(@"^[0-9a-f]{32}$");
    }

    // ── Storage failure atomicity ─────────────────────────────────────────────

    [Fact]
    public async Task Upload_Should_Not_Persist_Attachment_Record_When_Storage_Fails()
    {
        using var brokenFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IFileStorageService));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddSingleton<IFileStorageService>(new ThrowingFileStorageService());
            }));

        var (customerToken, _) = await SeedAndLoginAsync(UserRole.Customer);
        var ticketId = await CreateTicketAsync(customerToken);

        using var client = new HttpClient(brokenFactory.Server.CreateHandler())
        {
            BaseAddress = new Uri("https://localhost")
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", customerToken);

        using var form = BuildFileContent();
        var response = await client.PostAsync($"/api/tickets/{ticketId}/attachments", form);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attachments = await db.Set<TicketAttachment>()
            .Where(a => a.TicketId == ticketId)
            .ToListAsync();
        attachments.Should().BeEmpty();
    }

    private sealed class ThrowingFileStorageService : IFileStorageService
    {
        public string BuildPath(Guid ticketId, string fileName)
            => Path.Combine(Path.GetTempPath(), ticketId.ToString(), $"{Guid.NewGuid():N}");

        public Task SaveAsync(string storagePath, Stream content, CancellationToken ct = default)
            => throw new IOException("Simulated storage failure");

        public Task<Stream?> GetAsync(string storagePath, CancellationToken ct = default)
            => Task.FromResult<Stream?>(null);
    }
}
