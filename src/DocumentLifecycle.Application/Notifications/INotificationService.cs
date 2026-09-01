namespace DocumentLifecycle.Application.Notifications;

public interface INotificationService
{
    Task RefreshExpiryNotificationsAsync(CancellationToken cancellationToken = default);

    Task<NotificationCenter> GetForRoleAsync(
        string role,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        string role,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        Guid publicId,
        string role,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        string role,
        CancellationToken cancellationToken = default);
}

public sealed record NotificationCenter(
    IReadOnlyList<NotificationItem> Items,
    int UnreadCount);

public sealed record NotificationItem(
    Guid PublicId,
    string Message,
    string Link,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);
