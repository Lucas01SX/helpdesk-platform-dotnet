using Helpdesk.Modules.SLA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Modules.SLA.Infrastructure.Persistence.Configurations;

internal sealed class SlaMonthlyScoreConfiguration : IEntityTypeConfiguration<SlaMonthlyScore>
{
    public void Configure(EntityTypeBuilder<SlaMonthlyScore> builder)
    {
        builder.ToTable("sla_monthly_scores");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.Year).HasColumnName("year").IsRequired();
        builder.Property(s => s.Month).HasColumnName("month").IsRequired();
        builder.Property(s => s.Score).HasColumnName("score").IsRequired();
        builder.Property(s => s.TicketsWithinSla).HasColumnName("tickets_within_sla").IsRequired();
        builder.Property(s => s.TicketsBreached).HasColumnName("tickets_breached").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(s => new { s.Year, s.Month }).IsUnique();
    }
}
