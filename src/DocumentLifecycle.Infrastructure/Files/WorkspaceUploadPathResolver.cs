using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocumentLifecycle.Infrastructure.Files;

internal sealed class WorkspaceUploadPathResolver(
    IOptions<FileStorageOptions> options,
    IHostEnvironment environment)
{
    public string GetWorkspaceDirectory(Guid workspaceId)
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
        var relativePath = Path.GetRelativePath(rootPath, workspacePath);

        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("The workspace upload path is outside the configured root.");
        }

        return workspacePath;
    }

    public string GetFilePath(Guid workspaceId, string storedFilename)
    {
        if (string.IsNullOrWhiteSpace(storedFilename) ||
            !string.Equals(storedFilename, Path.GetFileName(storedFilename), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The stored filename is not a safe leaf filename.");
        }

        var workspacePath = GetWorkspaceDirectory(workspaceId);
        var filePath = Path.GetFullPath(Path.Combine(workspacePath, storedFilename));
        var relativePath = Path.GetRelativePath(workspacePath, filePath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("The stored file path is outside the workspace upload directory.");
        }

        return filePath;
    }
}
