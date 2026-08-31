using DocumentLifecycle.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentLifecycle.Infrastructure.Persistence.Configurations;

internal sealed class DocumentRevisionConfiguration : IEntityTypeConfiguration<DocumentRevision>
{
    public void Configure(EntityTypeBuilder<DocumentRevision> builder)
    {
        builder.ToTable("document_revisions");
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.PublicId).HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(revision => revision.WorkspaceId).HasColumnType("char(36)");
        builder.Property(revision => revision.ChangeNote).HasMaxLength(500).IsRequired();
        builder.Property(revision => revision.OriginalFilename).HasMaxLength(255).IsRequired();
        builder.Property(revision => revision.StoredFilename).HasMaxLength(80).IsRequired();
        builder.Property(revision => revision.MediaType).HasMaxLength(100).IsRequired();
        builder.Property(revision => revision.Size).HasColumnType("bigint");
        builder.Property(revision => revision.Sha256Hash)
            .HasColumnType("char(64)")
            .IsRequired();
        builder.Property(revision => revision.UploadedBy).HasMaxLength(256).IsRequired();
        builder.Property(revision => revision.UploadedAtUtc).HasColumnType("datetime(6)");
        builder.HasIndex(revision => new { revision.WorkspaceId, revision.PublicId }).IsUnique();
        builder.HasIndex(revision => new { revision.ManagedDocumentId, revision.RevisionNumber }).IsUnique();
        builder.HasIndex(revision => new { revision.WorkspaceId, revision.StoredFilename }).IsUnique();
    }
}
