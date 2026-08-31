namespace DocumentLifecycle.Application.Abstractions.Workspaces;

public interface IWorkspaceSeedService
{
    Task SeedAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
