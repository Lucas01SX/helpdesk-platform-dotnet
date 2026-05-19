using Helpdesk.API.Audit;
using Helpdesk.Modules.Identity;
using Helpdesk.Modules.Notifications;
using Helpdesk.Modules.SLA.Domain.Entities;
using Helpdesk.Modules.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.API.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(TicketsModule.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(IdentityModule.Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(NotificationsModule.Assembly);

        // SlaMonthlyScore configured inline to avoid EF Core 10 navigation-expander
        // IndexOutOfRangeException that occurs when ApplyConfigurationsFromAssembly
        // is called for a second assembly after queries have already been compiled.
        modelBuilder.Entity<SlaMonthlyScore>(b =>
        {
            b.ToTable("sla_monthly_scores");
            b.HasKey(s => s.Id);
            b.Property(s => s.Id).HasColumnName("id");
            b.Property(s => s.Year).HasColumnName("year").IsRequired();
            b.Property(s => s.Month).HasColumnName("month").IsRequired();
            b.Property(s => s.Score).HasColumnName("score").IsRequired();
            b.Property(s => s.TicketsWithinSla).HasColumnName("tickets_within_sla").IsRequired();
            b.Property(s => s.TicketsBreached).HasColumnName("tickets_breached").IsRequired();
            b.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
            b.HasIndex(s => new { s.Year, s.Month }).IsUnique();
        });
    }
}
