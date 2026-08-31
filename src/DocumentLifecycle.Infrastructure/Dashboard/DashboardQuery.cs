using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Dashboard;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Infrastructure.Dashboard;

internal sealed class DashboardQuery(
    ApplicationDbContext database,
    IClock clock) : IDashboardQuery
{
    public async Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var expiryThreshold = today.AddDays(30);
        var counts = await database.ManagedDocuments
            .GroupBy(_ => 1)
            .Select(group => new DashboardCounts(
                group.Count(),
                group.Count(document => document.State == LifecycleState.Draft),
                group.Count(document =>
                    document.State == LifecycleState.Active &&
                    (document.ExpiryDate == null || document.ExpiryDate > expiryThreshold)),
                group.Count(document =>
                    document.State == LifecycleState.Active &&
                    document.ExpiryDate >= today &&
                    document.ExpiryDate <= expiryThreshold),
                group.Count(document =>
                    document.State == LifecycleState.Active &&
                    document.ExpiryDate < today),
                group.Count(document => document.State == LifecycleState.Archived)))
            .SingleOrDefaultAsync(cancellationToken) ?? DashboardCounts.Empty;

        var recentActivity = await database.AuditEvents
            .AsNoTracking()
            .OrderByDescending(auditEvent => auditEvent.OccurredAtUtc)
            .ThenByDescending(auditEvent => auditEvent.Id)
            .Take(8)
            .Select(auditEvent => new RecentActivityItem(
                auditEvent.Actor,
                auditEvent.Action,
                auditEvent.EntityType,
                auditEvent.EntityPublicId,
                auditEvent.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        var expiringRows = await database.ManagedDocuments
            .AsNoTracking()
            .Where(document =>
                document.State == LifecycleState.Active &&
                document.ExpiryDate >= today &&
                document.ExpiryDate <= expiryThreshold)
            .OrderBy(document => document.ExpiryDate)
            .ThenBy(document => document.Code)
            .Select(document => new
            {
                document.PublicId,
                document.Code,
                document.Title,
                Category = document.Category.Name,
                Owner = document.Owner.DisplayName,
                ExpiryDate = document.ExpiryDate!.Value,
            })
            .ToListAsync(cancellationToken);
        var expiringDocuments = expiringRows
            .Select(document => new ExpiringDocumentItem(
                document.PublicId,
                document.Code,
                document.Title,
                document.Category,
                document.Owner,
                document.ExpiryDate,
                document.ExpiryDate.DayNumber - today.DayNumber))
            .ToList();

        return new DashboardSnapshot(
            counts.Total,
            counts.Draft,
            counts.Active,
            counts.ExpiringSoon,
            counts.Expired,
            counts.Archived,
            recentActivity,
            expiringDocuments);
    }

    private sealed record DashboardCounts(
        int Total,
        int Draft,
        int Active,
        int ExpiringSoon,
        int Expired,
        int Archived)
    {
        public static DashboardCounts Empty { get; } = new(0, 0, 0, 0, 0, 0);
    }
}
