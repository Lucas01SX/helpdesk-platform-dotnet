using Helpdesk.Modules.Tickets.Domain.Entities;
using Helpdesk.Modules.Tickets.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Modules.Tickets.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(t => t.AssigneeId).HasColumnName("assignee_id");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.SlaDueAt).HasColumnName("sla_due_at").IsRequired();

        builder.Property(t => t.PriorityChangeCount)
            .HasColumnName("priority_change_count")
            .IsRequired();

        builder.Property(t => t.TransferCount)
            .HasColumnName("transfer_count")
            .IsRequired();

        builder.Property(t => t.SlaBreachedAt).HasColumnName("sla_breached_at");
        builder.Property(t => t.AutoAssignedAt).HasColumnName("auto_assigned_at");
        builder.Property(t => t.SlaScoreApplied).HasColumnName("sla_score_applied").IsRequired();
        builder.Property(t => t.SlaExcluded).HasColumnName("sla_excluded").IsRequired();
        builder.Property(t => t.SlaUnassignedPenaltyCount)
            .HasColumnName("sla_unassigned_penalty_count")
            .IsRequired();

        builder.Ignore(t => t.DomainEvents);
    }
}
