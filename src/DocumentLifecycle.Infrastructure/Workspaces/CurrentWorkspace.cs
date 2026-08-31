using DocumentLifecycle.Application.Abstractions.Workspaces;

namespace DocumentLifecycle.Infrastructure.Workspaces;

public sealed class CurrentWorkspace : ICurrentWorkspace
{
    public Guid? WorkspaceId { get; private set; }

    public Guid GetRequiredWorkspaceId() => WorkspaceId
        ?? throw new InvalidOperationException("No demo workspace is available for the current request.");

    internal void Set(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(workspaceId));
        }

        if (WorkspaceId is not null && WorkspaceId != workspaceId)
        {
            throw new InvalidOperationException("The current request workspace cannot be replaced.");
        }

        WorkspaceId = workspaceId;
    }
}
