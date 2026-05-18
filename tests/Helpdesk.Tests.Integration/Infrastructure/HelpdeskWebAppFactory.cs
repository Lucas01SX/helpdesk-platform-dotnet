using Helpdesk.API.Persistence;
using Helpdesk.Modules.Identity.Application.Interfaces;
using Helpdesk.Modules.Identity.Domain.Entities;
using Helpdesk.Modules.Identity.Domain.Enums;
using Helpdesk.Shared.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Helpdesk.Tests.Integration.Infrastructure;

public sealed class HelpdeskWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("helpdesk_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public InMemoryEmailService EmailService { get; } = new();
    public FakeTimeProvider TimeProvider { get; } = new();

    // Returns a client whose cookies are managed automatically (required for session flows).
    // Uses https://localhost so the Secure cookie attribute is honoured.
    public HttpClient CreateClientWithCookies() =>
        new(new CookieContainerHandler { InnerHandler = Server.CreateHandler() })
        {
            BaseAddress = new Uri("https://localhost")
        };

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }

    // Seeds a user directly in the database — bypasses HTTP registration and email verification.
    // Agents and Managers are auto-verified by User.Create (role != Customer).
    public async Task<(string email, string password, Guid userId)> SeedUserAsync(UserRole role)
    {
        using var scope = Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = $"{Guid.NewGuid():N}@seed.test";
        const string password = "Secret1@pass";

        var now = DateTime.UtcNow;
        var user = User.Create(email, $"Seed {role}", hasher.Hash(password), role, now);
        user.VerifyEmail(now); // bypass email verification flow for test seeds
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        return (email, password, user.Id);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            var emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor is not null)
                services.Remove(emailDescriptor);

            services.AddSingleton<IEmailService>(EmailService);

            var clockDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDateTimeProvider));
            if (clockDescriptor is not null)
                services.Remove(clockDescriptor);

            services.AddSingleton<IDateTimeProvider>(TimeProvider);
        });
    }
}
