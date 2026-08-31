using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentLifecycle.Infrastructure.Workspaces;

public sealed class WorkspaceCleanupRunner(
    ApplicationDbContext database,
    IWorkspaceFileCleaner fileCleaner,
    IClock clock,
    ILogger<WorkspaceCleanupRunner> logger)
{
    private const int BatchSize = 50;

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var expiredWorkspaces = await database.DemoWorkspaces
            .Where(workspace => workspace.ExpiresAtUtc <= clock.UtcNow)
            .OrderBy(workspace => workspace.ExpiresAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        var removed = 0;

        foreach (var workspace in expiredWorkspaces)
        {
            try
            {
                await fileCleaner.DeleteWorkspaceFilesAsync(workspace.Id, cancellationToken);
                database.DemoWorkspaces.Remove(workspace);
                await database.SaveChangesAsync(cancellationToken);
                removed++;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                logger.LogInformation(
                    exception,
                    "Skipped workspace cleanup because the workspace changed concurrently.");
                database.Entry(workspace).State = EntityState.Detached;
            }
        }

        return removed;
    }
}
