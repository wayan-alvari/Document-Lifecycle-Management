using DocumentLifecycle.Domain.Notifications;
using DocumentLifecycle.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentLifecycle.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.PublicId).HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(notification => notification.WorkspaceId).HasColumnType("char(36)");
        builder.Property(notification => notification.RecipientRole).HasMaxLength(100);
        builder.Property(notification => notification.RecipientUserId).HasMaxLength(255);
        builder.Property(notification => notification.Message).HasMaxLength(500).IsRequired();
        builder.Property(notification => notification.Link).HasMaxLength(500).IsRequired();
        builder.Property(notification => notification.DeduplicationKey).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.CreatedAtUtc).HasColumnType("datetime(6)");
        builder.Property(notification => notification.ReadAtUtc).HasColumnType("datetime(6)");
        builder.HasIndex(notification => new { notification.WorkspaceId, notification.PublicId }).IsUnique();
        builder.HasIndex(notification => new
        {
            notification.WorkspaceId,
            notification.DeduplicationKey,
        }).IsUnique();
        builder.HasIndex(notification => new
        {
            notification.WorkspaceId,
            notification.RecipientRole,
            notification.ReadAtUtc,
        });
        builder.HasOne<DemoWorkspace>()
            .WithMany()
            .HasForeignKey(notification => notification.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
