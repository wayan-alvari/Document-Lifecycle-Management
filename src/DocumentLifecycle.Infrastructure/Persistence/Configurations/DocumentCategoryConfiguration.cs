using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentLifecycle.Infrastructure.Persistence.Configurations;

internal sealed class DocumentCategoryConfiguration : IEntityTypeConfiguration<DocumentCategory>
{
    public void Configure(EntityTypeBuilder<DocumentCategory> builder)
    {
        builder.ToTable("document_categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.PublicId).HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(category => category.WorkspaceId).HasColumnType("char(36)");
        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(500).IsRequired();
        builder.Property(category => category.IsActive).IsRequired();
        builder.HasIndex(category => new { category.WorkspaceId, category.PublicId }).IsUnique();
        builder.HasIndex(category => new { category.WorkspaceId, category.Name }).IsUnique();
        builder.HasOne<DemoWorkspace>()
            .WithMany()
            .HasForeignKey(category => category.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
