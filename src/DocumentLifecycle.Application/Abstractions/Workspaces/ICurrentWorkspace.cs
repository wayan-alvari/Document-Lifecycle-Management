namespace DocumentLifecycle.Application.Abstractions.Workspaces;

public interface ICurrentWorkspace
{
    Guid? WorkspaceId { get; }

    Guid GetRequiredWorkspaceId();
}
