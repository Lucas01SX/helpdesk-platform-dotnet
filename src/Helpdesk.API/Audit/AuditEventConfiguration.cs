using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.API.Audit;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(100);
        builder.Property(e => e.AggregateType).HasColumnName("aggregate_type").IsRequired().HasMaxLength(50);
        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id");
        builder.Property(e => e.Payload).HasColumnName("payload").IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);

        builder.HasIndex(e => e.AggregateId).HasDatabaseName("ix_audit_events_aggregate_id");
        builder.HasIndex(e => e.EventType).HasDatabaseName("ix_audit_events_event_type");
        builder.HasIndex(e => e.OccurredAt).HasDatabaseName("ix_audit_events_occurred_at");
        builder.HasIndex(e => new { e.AggregateId, e.OccurredAt })
               .HasDatabaseName("ix_audit_events_aggregate_id_occurred_at");
    }
}
