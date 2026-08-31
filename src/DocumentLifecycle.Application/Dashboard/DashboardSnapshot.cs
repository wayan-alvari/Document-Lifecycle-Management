namespace DocumentLifecycle.Application.Dashboard;

public sealed record DashboardSnapshot(
    int Total,
    int Draft,
    int Active,
    int ExpiringSoon,
    int Expired,
    int Archived,
    IReadOnlyList<RecentActivityItem> RecentActivity,
    IReadOnlyList<ExpiringDocumentItem> ExpiringDocuments);

public sealed record RecentActivityItem(
    string Actor,
    string Action,
    string EntityType,
    Guid EntityPublicId,
    DateTime OccurredAtUtc);

public sealed record ExpiringDocumentItem(
    Guid PublicId,
    string Code,
    string Title,
    string Category,
    string Owner,
    DateOnly ExpiryDate,
    int DaysRemaining);
