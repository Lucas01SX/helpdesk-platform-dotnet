using Helpdesk.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Helpdesk.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.FamilyId).IsRequired();
        builder.Property(s => s.RefreshTokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(s => s.RefreshTokenHash).IsUnique();
        builder.Property(s => s.UserAgent).HasMaxLength(512);
        builder.Property(s => s.IpAddress).HasMaxLength(45);
        builder.Property(s => s.ExpiresAt).IsRequired();
        builder.Property(s => s.RevokedAt);
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => new { s.UserId, s.RevokedAt });
        builder.HasIndex(s => s.FamilyId);
    }
}
