using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Domain.Activity;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Domain.Notifications;
using DocumentLifecycle.Domain.Workspaces;
using DocumentLifecycle.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentWorkspace currentWorkspace)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<DemoWorkspace> DemoWorkspaces => Set<DemoWorkspace>();

    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();

    public DbSet<DocumentOwner> DocumentOwners => Set<DocumentOwner>();

    public DbSet<ManagedDocument> ManagedDocuments => Set<ManagedDocument>();

    public DbSet<DocumentRevision> DocumentRevisions => Set<DocumentRevision>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.DisplayName)
                .HasMaxLength(120)
                .IsRequired();
        });

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.Entity<DocumentCategory>().HasQueryFilter(entity =>
            currentWorkspace.WorkspaceId != null && entity.WorkspaceId == currentWorkspace.WorkspaceId.Value);
        builder.Entity<DocumentOwner>().HasQueryFilter(entity =>
            currentWorkspace.WorkspaceId != null && entity.WorkspaceId == currentWorkspace.WorkspaceId.Value);
        builder.Entity<ManagedDocument>().HasQueryFilter(entity =>
            currentWorkspace.WorkspaceId != null && entity.WorkspaceId == currentWorkspace.WorkspaceId.Value);
        builder.Entity<DocumentRevision>().HasQueryFilter(entity =>
            currentWorkspace.WorkspaceId != null && entity.WorkspaceId == currentWorkspace.WorkspaceId.Value);
        builder.Entity<Notification>().HasQueryFilter(entity =>
            currentWorkspace.WorkspaceId != null && entity.WorkspaceId == currentWorkspace.WorkspaceId.Value);
        builder.Entity<AuditEvent>().HasQueryFilter(entity =>
            currentWorkspace.WorkspaceId != null && entity.WorkspaceId == currentWorkspace.WorkspaceId.Value);

        if (Database.IsMySql())
        {
            builder.UseCollation("utf8mb4_0900_ai_ci");
            builder.Entity<DocumentRevision>()
                .Property(revision => revision.Sha256Hash)
                .UseCollation("ascii_general_ci");
        }

        builder.UseSnakeCaseNames();
    }
}
