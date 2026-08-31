namespace DocumentLifecycle.Domain.Common;

public interface IWorkspaceScoped
{
    Guid WorkspaceId { get; }
}
