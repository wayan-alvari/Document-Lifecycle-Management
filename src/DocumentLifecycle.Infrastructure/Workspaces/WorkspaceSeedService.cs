using DocumentLifecycle.Application.Abstractions.Workspaces;

namespace DocumentLifecycle.Infrastructure.Workspaces;

internal sealed class WorkspaceSeedService : IWorkspaceSeedService
{
    public Task SeedAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
