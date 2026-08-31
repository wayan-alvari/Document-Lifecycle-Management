using DocumentLifecycle.Domain.Activity;
using DocumentLifecycle.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentLifecycle.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.PublicId).HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(auditEvent => auditEvent.WorkspaceId).HasColumnType("char(36)");
        builder.Property(auditEvent => auditEvent.Actor).HasMaxLength(256).IsRequired();
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(100).IsRequired();
        builder.Property(auditEvent => auditEvent.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(auditEvent => auditEvent.EntityPublicId).HasColumnType("char(36)");
        builder.Property(auditEvent => auditEvent.OccurredAtUtc).HasColumnType("datetime(6)");
        builder.Property(auditEvent => auditEvent.DetailsJson).HasColumnType("json").IsRequired();
        builder.HasIndex(auditEvent => new { auditEvent.WorkspaceId, auditEvent.PublicId }).IsUnique();
        builder.HasIndex(auditEvent => new { auditEvent.WorkspaceId, auditEvent.OccurredAtUtc });
        builder.HasIndex(auditEvent => new
        {
            auditEvent.WorkspaceId,
            auditEvent.EntityType,
            auditEvent.EntityPublicId,
        });
        builder.HasOne<DemoWorkspace>()
            .WithMany()
            .HasForeignKey(auditEvent => auditEvent.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
