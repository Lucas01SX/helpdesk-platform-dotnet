using System.Reflection;
using Helpdesk.Modules.Tickets.Application.UseCases;
using Helpdesk.Modules.Tickets.Domain.Interfaces;
using Helpdesk.Modules.Tickets.Infrastructure.Persistence.Repositories;
using Helpdesk.Modules.Tickets.Infrastructure.Queries;
using Helpdesk.Modules.Tickets.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Modules.Tickets;

public static class TicketsModule
{
    public static readonly Assembly Assembly = typeof(TicketsModule).Assembly;

    public static IServiceCollection AddTicketsModule(this IServiceCollection services)
    {
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketCommentRepository, TicketCommentRepository>();
        services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<TicketQueryService>();

        services.AddScoped<CreateTicketUseCase>();
        services.AddScoped<AssignTicketUseCase>();
        services.AddScoped<ResolveTicketUseCase>();
        services.AddScoped<CancelTicketUseCase>();
        services.AddScoped<TransferTicketUseCase>();
        services.AddScoped<ChangePriorityUseCase>();
        services.AddScoped<AddCommentUseCase>();
        services.AddScoped<UploadAttachmentUseCase>();
        services.AddScoped<GetAttachmentFileUseCase>();

        return services;
    }
}
