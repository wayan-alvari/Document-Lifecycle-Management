namespace DocumentLifecycle.Application.Abstractions.Workspaces;

public interface IWorkspaceFileCleaner
{
    Task DeleteWorkspaceFilesAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
