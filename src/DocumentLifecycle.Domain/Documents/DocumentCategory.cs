using DocumentLifecycle.Domain.Common;

namespace DocumentLifecycle.Domain.Documents;

public sealed class DocumentCategory : IWorkspaceScoped
{
    private DocumentCategory()
    {
    }

    private DocumentCategory(Guid workspaceId, string name, string description)
    {
        WorkspaceId = workspaceId;
        PublicId = Guid.NewGuid();
        Name = Required(name, nameof(name), 100);
        Description = Optional(description, 500);
        IsActive = true;
    }

    public long Id { get; private set; }

    public Guid PublicId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public static DocumentCategory Create(Guid workspaceId, string name, string description)
    {
        EnsureWorkspace(workspaceId);
        return new DocumentCategory(workspaceId, name, description);
    }

    public void Update(string name, string description)
    {
        Name = Required(name, nameof(name), 100);
        Description = Optional(description, 500);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static void EnsureWorkspace(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(workspaceId));
        }
    }

    private static string Required(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value must contain 1 to {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string Optional(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", nameof(value));
        }

        return normalized;
    }
}
