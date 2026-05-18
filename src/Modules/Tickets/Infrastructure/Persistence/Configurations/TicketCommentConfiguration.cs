using Helpdesk.Modules.Tickets.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Modules.Tickets.Infrastructure.Persistence.Configurations;

internal sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.ToTable("ticket_comments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(c => c.AuthorId).HasColumnName("author_id").IsRequired();

        builder.Property(c => c.Content)
            .HasColumnName("content")
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(c => c.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
