using System.Reflection;
using Helpdesk.Modules.SLA.Application.UseCases;
using Helpdesk.Modules.SLA.Domain.Interfaces;
using Helpdesk.Modules.SLA.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Modules.SLA;

public static class SlaModule
{
    public static readonly Assembly Assembly = typeof(SlaModule).Assembly;

    public static IServiceCollection AddSlaModule(this IServiceCollection services)
    {
        services.AddScoped<ISlaScoreRepository, SlaScoreRepository>();
        services.AddScoped<GetSlaScoresUseCase>();
        return services;
    }
}
