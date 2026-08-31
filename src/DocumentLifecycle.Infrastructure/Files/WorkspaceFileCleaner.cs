using DocumentLifecycle.Application.Abstractions.Workspaces;

namespace DocumentLifecycle.Infrastructure.Files;

internal sealed class WorkspaceFileCleaner(
    WorkspaceUploadPathResolver pathResolver) : IWorkspaceFileCleaner
{
    public async Task DeleteWorkspaceFilesAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var workspacePath = pathResolver.GetWorkspaceDirectory(workspaceId);

        if (!Directory.Exists(workspacePath))
        {
            return;
        }

        await Task.Run(
            () => Directory.Delete(workspacePath, recursive: true),
            cancellationToken);
    }
}
