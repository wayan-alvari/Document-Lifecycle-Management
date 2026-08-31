using System.Collections.Concurrent;
using System.Data;
using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Domain.Workspaces;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocumentLifecycle.Infrastructure.Workspaces;

public sealed class WorkspaceCoordinator(
    ApplicationDbContext database,
    CurrentWorkspace currentWorkspace,
    IWorkspaceSeedService seedService,
    IWorkspaceFileCleaner fileCleaner,
    IClock clock,
    IOptions<DemoModeOptions> options)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> WorkspaceLocks = new();

    public async Task<WorkspaceResolution> ResolveAsync(
        Guid? requestedWorkspaceId,
        bool meaningfulActivity,
        CancellationToken cancellationToken = default)
    {
        var lockKey = requestedWorkspaceId ?? Guid.NewGuid();
        var workspaceLock = WorkspaceLocks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await workspaceLock.WaitAsync(cancellationToken);

        try
        {
            return await ResolveWithinLockAsync(
                requestedWorkspaceId,
                meaningfulActivity,
                cancellationToken);
        }
        finally
        {
            workspaceLock.Release();
            WorkspaceLocks.TryRemove(new KeyValuePair<Guid, SemaphoreSlim>(lockKey, workspaceLock));
        }
    }

    private async Task<WorkspaceResolution> ResolveWithinLockAsync(
        Guid? requestedWorkspaceId,
        bool meaningfulActivity,
        CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;
        var created = false;
        var reset = false;
        var activityRecorded = false;

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        DemoWorkspace? workspace = null;
        if (requestedWorkspaceId is not null)
        {
            workspace = await database.DemoWorkspaces.SingleOrDefaultAsync(
                candidate => candidate.Id == requestedWorkspaceId,
                cancellationToken);
        }

        if (workspace is not null && workspace.IsExpired(utcNow))
        {
            database.DemoWorkspaces.Remove(workspace);
            await database.SaveChangesAsync(cancellationToken);
            await fileCleaner.DeleteWorkspaceFilesAsync(workspace.Id, cancellationToken);
            workspace = null;
            reset = true;
        }

        if (workspace is null)
        {
            workspace = DemoWorkspace.Create(Guid.NewGuid(), utcNow, options.Value.SeedVersion);
            database.DemoWorkspaces.Add(workspace);
            currentWorkspace.Set(workspace.Id);
            await seedService.SeedAsync(workspace.Id, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            created = true;
        }
        else
        {
            currentWorkspace.Set(workspace.Id);
            activityRecorded = meaningfulActivity && workspace.RecordMeaningfulActivity(
                utcNow,
                options.Value.ActivityWriteInterval);

            if (activityRecorded)
            {
                await database.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return new WorkspaceResolution(workspace.Id, created, reset, activityRecorded);
    }
}
