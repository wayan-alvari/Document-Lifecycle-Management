using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentLifecycle.Infrastructure.Persistence.Configurations;

internal sealed class ManagedDocumentConfiguration : IEntityTypeConfiguration<ManagedDocument>
{
    public void Configure(EntityTypeBuilder<ManagedDocument> builder)
    {
        builder.ToTable("managed_documents");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.PublicId).HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(document => document.WorkspaceId).HasColumnType("char(36)");
        builder.Property(document => document.Code).HasMaxLength(40).IsRequired();
        builder.Property(document => document.Title).HasMaxLength(200).IsRequired();
        builder.Property(document => document.Description).HasMaxLength(2000).IsRequired();
        builder.Property(document => document.EffectiveDate).HasColumnType("date");
        builder.Property(document => document.ExpiryDate).HasColumnType("date");
        builder.Property(document => document.State)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(document => document.ArchiveReason).HasMaxLength(500);
        builder.Property(document => document.ArchivedBy).HasMaxLength(256);
        builder.Property(document => document.ArchivedAtUtc).HasColumnType("datetime(6)");
        builder.Property(document => document.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(document => document.CreatedAtUtc).HasColumnType("datetime(6)");
        builder.Property(document => document.UpdatedBy).HasMaxLength(256).IsRequired();
        builder.Property(document => document.UpdatedAtUtc).HasColumnType("datetime(6)");
        builder.HasIndex(document => new { document.WorkspaceId, document.PublicId }).IsUnique();
        builder.HasIndex(document => new { document.WorkspaceId, document.Code }).IsUnique();
        builder.HasIndex(document => new { document.WorkspaceId, document.State });
        builder.HasIndex(document => new { document.WorkspaceId, document.ExpiryDate });
        builder.HasIndex(document => new { document.WorkspaceId, document.CategoryId });
        builder.HasIndex(document => new { document.WorkspaceId, document.OwnerId });
        builder.HasOne<DemoWorkspace>()
            .WithMany()
            .HasForeignKey(document => document.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(document => document.Category)
            .WithMany()
            .HasForeignKey(document => document.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(document => document.Owner)
            .WithMany()
            .HasForeignKey(document => document.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(document => document.Revisions)
            .WithOne()
            .HasForeignKey(revision => revision.ManagedDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(document => document.Revisions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
