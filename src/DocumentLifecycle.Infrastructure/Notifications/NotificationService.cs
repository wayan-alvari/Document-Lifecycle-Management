using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Application.Notifications;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Domain.Notifications;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Infrastructure.Notifications;

internal sealed class NotificationService(
    ApplicationDbContext database,
    ICurrentWorkspace currentWorkspace,
    IClock clock) : INotificationService
{
    private static readonly SemaphoreSlim GenerationLock = new(1, 1);

    public async Task RefreshExpiryNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var workspaceId = currentWorkspace.WorkspaceId ??
            throw new InvalidOperationException("A current workspace is required.");
        await GenerationLock.WaitAsync(cancellationToken);

        try
        {
            var today = DateOnly.FromDateTime(clock.UtcNow);
            var threshold = today.AddDays(30);
            var documents = await database.ManagedDocuments
                .AsNoTracking()
                .Where(document =>
                    document.State == LifecycleState.Active &&
                    document.ExpiryDate != null &&
                    document.ExpiryDate <= threshold)
                .Select(document => new
                {
                    document.PublicId,
                    document.Code,
                    document.Title,
                    ExpiryDate = document.ExpiryDate!.Value,
                })
                .ToListAsync(cancellationToken);
            var existingKeyList = await database.Notifications
                .AsNoTracking()
                .Select(notification => notification.DeduplicationKey)
                .ToListAsync(cancellationToken);
            var existingKeys = existingKeyList.ToHashSet(StringComparer.Ordinal);
            var createdAt = clock.UtcNow;

            foreach (var document in documents)
            {
                var status = document.ExpiryDate < today ? "expired" : "expiring";
                foreach (var role in ApplicationRoles.All)
                {
                    var key = $"expiry:{document.PublicId:N}:{document.ExpiryDate:yyyyMMdd}:{status}:{role}";
                    if (!existingKeys.Add(key))
                    {
                        continue;
                    }

                    var message = status == "expired"
                        ? $"{document.Code} - {document.Title} passed its review date on {document.ExpiryDate:dd MMM yyyy}."
                        : $"{document.Code} - {document.Title} is due for review on {document.ExpiryDate:dd MMM yyyy}.";
                    database.Notifications.Add(Notification.Create(
                        workspaceId,
                        role,
                        recipientUserId: null,
                        message,
                        $"/Documents/Details/{document.PublicId}",
                        key,
                        createdAt));
                }
            }

            if (database.ChangeTracker.HasChanges())
            {
                await database.SaveChangesAsync(cancellationToken);
            }
        }
        finally
        {
            GenerationLock.Release();
        }
    }

    public async Task<NotificationCenter> GetForRoleAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        var items = await database.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientRole == role)
            .OrderBy(notification => notification.ReadAtUtc != null)
            .ThenByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Select(notification => new NotificationItem(
                notification.PublicId,
                notification.Message,
                notification.Link,
                notification.CreatedAtUtc,
                notification.ReadAtUtc))
            .ToListAsync(cancellationToken);
        return new NotificationCenter(items, items.Count(item => item.ReadAtUtc is null));
    }

    public Task<int> GetUnreadCountAsync(
        string role,
        CancellationToken cancellationToken = default) =>
        database.Notifications.CountAsync(
            notification => notification.RecipientRole == role && notification.ReadAtUtc == null,
            cancellationToken);

    public async Task<bool> MarkReadAsync(
        Guid publicId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var notification = await database.Notifications.SingleOrDefaultAsync(
            item => item.PublicId == publicId && item.RecipientRole == role,
            cancellationToken);
        if (notification is null)
        {
            return false;
        }

        notification.MarkRead(clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        var notifications = await database.Notifications
            .Where(notification => notification.RecipientRole == role && notification.ReadAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            notification.MarkRead(clock.UtcNow);
        }

        if (notifications.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        return notifications.Count;
    }
}
