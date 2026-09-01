namespace DocumentLifecycle.Application.Audit;

public interface IAuditQuery
{
    Task<AuditListPage> GetAsync(
        AuditListFilter filter,
        CancellationToken cancellationToken = default);
}

public sealed record AuditListFilter(
    string? Search,
    string? Action,
    int Page = 1,
    int PageSize = 15);

public sealed record AuditListPage(
    IReadOnlyList<AuditListItem> Items,
    IReadOnlyList<string> AvailableActions,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record AuditListItem(
    Guid PublicId,
    string Actor,
    string Action,
    string EntityType,
    Guid EntityPublicId,
    DateTime OccurredAtUtc,
    string Summary);
