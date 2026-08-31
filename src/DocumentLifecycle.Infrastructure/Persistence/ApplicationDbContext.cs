using DocumentLifecycle.Domain.Workspaces;
using DocumentLifecycle.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<DemoWorkspace> DemoWorkspaces => Set<DemoWorkspace>();

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

        if (Database.IsMySql())
        {
            builder.UseCollation("utf8mb4_0900_ai_ci");
        }

        builder.UseSnakeCaseNames();
    }
}
