using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Helpdesk.API;
using Helpdesk.API.Audit;
using Helpdesk.API.Controllers;
using Helpdesk.API.Middleware;
using Helpdesk.Shared.Audit;
using Helpdesk.API.Persistence;
using Helpdesk.API.SLA;
using Helpdesk.Modules.Identity;
using Helpdesk.Modules.Identity.Infrastructure.Security;
using Helpdesk.Modules.SLA;
using Helpdesk.Modules.Tickets;
using Helpdesk.Shared.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        })
        .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = ctx =>
            {
                var correlationId = ctx.HttpContext.Items["CorrelationId"] as string
                    ?? ctx.HttpContext.TraceIdentifier;
                var response = new ApiFailureResponse(
                    false,
                    new ApiErrorDetail("validation_error", "One or more validation errors occurred."),
                    correlationId,
                    DateTime.UtcNow);
                return new BadRequestObjectResult(response);
            };
        });
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, ct) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "Helpdesk Platform API",
                Version = "v1",
                Description = "Helpdesk ticket management API — portfolio project demonstrating Clean Architecture with .NET 10."
            };
            document.Components ??= new OpenApiComponents();
            if (document.Components.SecuritySchemes is null)
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT access token obtained from POST /api/auth/sessions."
            };

            var bearerRef = new OpenApiSecuritySchemeReference("Bearer", document);
            var securityRequirement = new OpenApiSecurityRequirement { [bearerRef] = [] };

            foreach (var path in document.Paths.Values)
                foreach (var operation in (path.Operations ?? []).Values)
                {
                    operation.Security ??= [];
                    operation.Security.Add(securityRequirement);
                }

            return Task.CompletedTask;
        });
    });

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var origins = builder.Environment.IsProduction()
                ? ["https://lucas01sx.github.io"]
                : (string[])["https://lucas01sx.github.io", "http://localhost:4200"];

            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    // Register abstract DbContext so module repositories can receive it via DI
    builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

    builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
    builder.Services.AddSingleton<IAuditService, AuditService>();

    builder.Services.AddIdentityModule(builder.Configuration);
    builder.Services.AddTicketsModule();
    builder.Services.AddSlaModule();

    builder.Services.AddScoped<SlaBreachProcessor>();
    builder.Services.AddHostedService<SlaBreachMonitorService>();

    // JWT authentication
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var jwt = builder.Configuration
                .GetSection(JwtSettings.SectionName)
                .Get<JwtSettings>()!;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    // Rate limiting — disabled in the Test environment so functional integration tests
    // don't exhaust per-IP counters. AuthRateLimitTests uses a separate factory that
    // runs under the Development environment where rate limiting is active.
    if (!builder.Environment.IsEnvironment("Test"))
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicies.Login, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.PasswordReset, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.ContentType = "application/json";
                await ctx.HttpContext.Response.WriteAsync(
                    """{"success":false,"error":{"code":"rate_limit_exceeded","message":"Too many requests. Please try again later."}}""",
                    ct);
            };
        });
    }

    var app = builder.Build();

    // Apply any pending EF Core migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "Helpdesk Platform API";
            options.DefaultHttpClient = new(ScalarTarget.Shell, ScalarClient.Curl);
        });
    }

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    app.UseCorrelationId();
    app.UseSecurityHeaders();
    app.UseCors();
    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging();
    if (!app.Environment.IsEnvironment("Test"))
        app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
    {
        try
        {
            await db.Database.CanConnectAsync(ct);
            return Results.Ok(new { status = "healthy", database = "connected", timestamp = DateTime.UtcNow });
        }
        catch
        {
            return Results.Json(
                new { status = "unhealthy", database = "unavailable", timestamp = DateTime.UtcNow },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }).AllowAnonymous();

    if (app.Environment.IsEnvironment("Test"))
        app.MapGet("/test/throw", (HttpContext _) => throw new InvalidOperationException("Test exception from M7"))
           .AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
