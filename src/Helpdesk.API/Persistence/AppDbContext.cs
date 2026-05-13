using Helpdesk.Modules.Identity;
using Helpdesk.Modules.Notifications;
using Helpdesk.Modules.SLA;
using Helpdesk.Modules.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(TicketsModule.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(IdentityModule.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(SlaModule.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(NotificationsModule.Assembly);
    }
}
