using DocumentLifecycle.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentLifecycle.Infrastructure.Persistence.Configurations;

internal sealed class DemoWorkspaceConfiguration : IEntityTypeConfiguration<DemoWorkspace>
{
    public void Configure(EntityTypeBuilder<DemoWorkspace> builder)
    {
        builder.ToTable("demo_workspaces");
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Id)
            .HasColumnType("char(36)")
            .ValueGeneratedNever();
        builder.Property(workspace => workspace.CreatedAtUtc)
            .HasColumnType("datetime(6)");
        builder.Property(workspace => workspace.LastActivityAtUtc)
            .HasColumnType("datetime(6)");
        builder.Property(workspace => workspace.ExpiresAtUtc)
            .HasColumnType("datetime(6)");
        builder.Property(workspace => workspace.SeedVersion)
            .IsRequired();
        builder.Property(workspace => workspace.Version)
            .IsConcurrencyToken();
        builder.HasIndex(workspace => workspace.ExpiresAtUtc);
    }
}
