using Helpdesk.Shared.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Modules.Notifications;

public static class NotificationsExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddSingleton<INotificationService, LogNotificationService>();
        return services;
    }
}
