using Helpdesk.Modules.Tickets.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Modules.Tickets.Infrastructure.Persistence.Configurations;

internal sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("ticket_attachments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(a => a.UploadedBy).HasColumnName("uploaded_by").IsRequired();

        builder.Property(a => a.FileName)
            .HasColumnName("file_name")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.StoragePath)
            .HasColumnName("storage_path")
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.ContentType)
            .HasColumnName("content_type")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.SizeBytes).HasColumnName("size_bytes").IsRequired();

        builder.Property(a => a.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
