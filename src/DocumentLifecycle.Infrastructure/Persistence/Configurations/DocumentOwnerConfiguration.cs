using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentLifecycle.Infrastructure.Persistence.Configurations;

internal sealed class DocumentOwnerConfiguration : IEntityTypeConfiguration<DocumentOwner>
{
    public void Configure(EntityTypeBuilder<DocumentOwner> builder)
    {
        builder.ToTable("document_owners");
        builder.HasKey(owner => owner.Id);
        builder.Property(owner => owner.PublicId).HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(owner => owner.WorkspaceId).HasColumnType("char(36)");
        builder.Property(owner => owner.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(owner => owner.Contact).HasMaxLength(160).IsRequired();
        builder.Property(owner => owner.IsActive).IsRequired();
        builder.HasIndex(owner => new { owner.WorkspaceId, owner.PublicId }).IsUnique();
        builder.HasIndex(owner => new { owner.WorkspaceId, owner.DisplayName }).IsUnique();
        builder.HasOne<DemoWorkspace>()
            .WithMany()
            .HasForeignKey(owner => owner.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
