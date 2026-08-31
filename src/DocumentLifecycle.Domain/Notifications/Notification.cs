using DocumentLifecycle.Domain.Common;

namespace DocumentLifecycle.Domain.Notifications;

public sealed class Notification : IWorkspaceScoped
{
    private Notification()
    {
    }

    private Notification(
        Guid workspaceId,
        string? recipientRole,
        string? recipientUserId,
        string message,
        string link,
        string deduplicationKey,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(recipientRole) && string.IsNullOrWhiteSpace(recipientUserId))
        {
            throw new ArgumentException("A recipient role or user is required.");
        }

        WorkspaceId = workspaceId;
        PublicId = Guid.NewGuid();
        RecipientRole = Optional(recipientRole, 100);
        RecipientUserId = Optional(recipientUserId, 255);
        Message = Required(message, nameof(message), 500);
        Link = Required(link, nameof(link), 500);
        DeduplicationKey = Required(deduplicationKey, nameof(deduplicationKey), 200);
        CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc
            : throw new ArgumentException("Notification timestamps must be UTC.", nameof(createdAtUtc));
    }

    public long Id { get; private set; }

    public Guid PublicId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string? RecipientRole { get; private set; }

    public string? RecipientUserId { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public string Link { get; private set; } = string.Empty;

    public string DeduplicationKey { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public static Notification Create(
        Guid workspaceId,
        string? recipientRole,
        string? recipientUserId,
        string message,
        string link,
        string deduplicationKey,
        DateTime createdAtUtc)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(workspaceId));
        }

        return new Notification(
            workspaceId,
            recipientRole,
            recipientUserId,
            message,
            link,
            deduplicationKey,
            createdAtUtc);
    }

    public void MarkRead(DateTime readAtUtc)
    {
        if (ReadAtUtc is not null)
        {
            return;
        }

        ReadAtUtc = readAtUtc.Kind == DateTimeKind.Utc
            ? readAtUtc
            : throw new ArgumentException("Notification timestamps must be UTC.", nameof(readAtUtc));
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

    private static string? Optional(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", nameof(value));
        }

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
