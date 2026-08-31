using DocumentLifecycle.Domain.Common;

namespace DocumentLifecycle.Domain.Activity;

public sealed class AuditEvent : IWorkspaceScoped
{
    private AuditEvent()
    {
    }

    private AuditEvent(
        Guid workspaceId,
        string actor,
        string action,
        string entityType,
        Guid entityPublicId,
        DateTime occurredAtUtc,
        string detailsJson)
    {
        WorkspaceId = workspaceId;
        PublicId = Guid.NewGuid();
        Actor = Required(actor, nameof(actor), 256);
        Action = Required(action, nameof(action), 100);
        EntityType = Required(entityType, nameof(entityType), 100);
        EntityPublicId = entityPublicId != Guid.Empty
            ? entityPublicId
            : throw new ArgumentException("An entity public ID is required.", nameof(entityPublicId));
        OccurredAtUtc = occurredAtUtc.Kind == DateTimeKind.Utc
            ? occurredAtUtc
            : throw new ArgumentException("Audit timestamps must be UTC.", nameof(occurredAtUtc));
        DetailsJson = Required(detailsJson, nameof(detailsJson), 4000);
    }

    public long Id { get; private set; }

    public Guid PublicId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Actor { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityPublicId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string DetailsJson { get; private set; } = "{}";

    public static AuditEvent Create(
        Guid workspaceId,
        string actor,
        string action,
        string entityType,
        Guid entityPublicId,
        DateTime occurredAtUtc,
        string detailsJson = "{}")
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(workspaceId));
        }

        return new AuditEvent(
            workspaceId,
            actor,
            action,
            entityType,
            entityPublicId,
            occurredAtUtc,
            detailsJson);
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
}
