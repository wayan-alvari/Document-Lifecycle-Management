using DocumentLifecycle.Domain.Common;

namespace DocumentLifecycle.Domain.Documents;

public sealed class DocumentOwner : IWorkspaceScoped
{
    private DocumentOwner()
    {
    }

    private DocumentOwner(Guid workspaceId, string displayName, string contact)
    {
        WorkspaceId = workspaceId;
        PublicId = Guid.NewGuid();
        DisplayName = Required(displayName, nameof(displayName), 120);
        Contact = Required(contact, nameof(contact), 160);
        IsActive = true;
    }

    public long Id { get; private set; }

    public Guid PublicId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Contact { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public static DocumentOwner Create(Guid workspaceId, string displayName, string contact)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(workspaceId));
        }

        return new DocumentOwner(workspaceId, displayName, contact);
    }

    public void Update(string displayName, string contact)
    {
        DisplayName = Required(displayName, nameof(displayName), 120);
        Contact = Required(contact, nameof(contact), 160);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private static string Required(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value must contain 1 to {maximumLength} characters.", parameterName);
        }

        return normalized;
    }
}
