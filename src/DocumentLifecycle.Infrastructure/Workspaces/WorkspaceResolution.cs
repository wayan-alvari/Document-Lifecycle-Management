namespace DocumentLifecycle.Infrastructure.Workspaces;

public sealed record WorkspaceResolution(
    Guid WorkspaceId,
    bool Created,
    bool Reset,
    bool ActivityRecorded);
