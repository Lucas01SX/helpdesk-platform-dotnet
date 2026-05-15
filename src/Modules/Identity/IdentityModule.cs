using System.Reflection;
using Helpdesk.Modules.Identity.Application.Interfaces;
using Helpdesk.Modules.Identity.Application.UseCases;
using Helpdesk.Modules.Identity.Domain.Interfaces;
using Helpdesk.Modules.Identity.Infrastructure.Persistence.Repositories;
using Helpdesk.Modules.Identity.Infrastructure.Security;
using Helpdesk.Modules.Identity.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Modules.Identity;

public static class IdentityModule
{
    public static readonly Assembly Assembly = typeof(IdentityModule).Assembly;

    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailService, LogEmailService>();

        services.AddScoped<RegisterUseCase>();
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<LogoutUseCase>();
        services.AddScoped<VerifyEmailUseCase>();
        services.AddScoped<RequestPasswordResetUseCase>();
        services.AddScoped<ResetPasswordUseCase>();

        return services;
    }
}
