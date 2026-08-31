using DocumentLifecycle.Application.Abstractions.Workspaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocumentLifecycle.Infrastructure.Files;

internal sealed class WorkspaceFileCleaner(
    IOptions<FileStorageOptions> options,
    IHostEnvironment environment) : IWorkspaceFileCleaner
{
    public async Task DeleteWorkspaceFilesAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(workspaceId));
        }

        var configuredRoot = options.Value.RootPath;
        var rootPath = Path.GetFullPath(
            Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(environment.ContentRootPath, configuredRoot));
        var workspacePath = Path.GetFullPath(Path.Combine(rootPath, workspaceId.ToString("N")));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!workspacePath.StartsWith(rootPath + Path.DirectorySeparatorChar, comparison))
        {
            throw new InvalidOperationException("The workspace upload path is outside the configured root.");
        }

        if (!Directory.Exists(workspacePath))
        {
            return;
        }

        await Task.Run(
            () => Directory.Delete(workspacePath, recursive: true),
            cancellationToken);
    }
}
