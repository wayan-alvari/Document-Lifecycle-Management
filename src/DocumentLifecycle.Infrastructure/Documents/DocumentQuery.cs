using DocumentLifecycle.Application.Documents;
using DocumentLifecycle.Domain.Documents;

namespace DocumentLifecycle.Infrastructure.Documents;

internal static class DocumentQuery
{
    public static IQueryable<ManagedDocument> ApplyFilters(
        IQueryable<ManagedDocument> query,
        DocumentListFilter filter,
        bool includeDrafts,
        DateOnly today)
    {
        if (!includeDrafts)
        {
            query = query.Where(document => document.State != LifecycleState.Draft);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(document =>
                document.Code.Contains(search) ||
                document.Title.Contains(search) ||
                document.Description.Contains(search));
        }

        if (filter.CategoryId is not null)
        {
            query = query.Where(document => document.Category.PublicId == filter.CategoryId);
        }

        if (filter.OwnerId is not null)
        {
            query = query.Where(document => document.Owner.PublicId == filter.OwnerId);
        }

        if (filter.ExpiryFrom is not null)
        {
            query = query.Where(document => document.ExpiryDate >= filter.ExpiryFrom);
        }

        if (filter.ExpiryTo is not null)
        {
            query = query.Where(document => document.ExpiryDate <= filter.ExpiryTo);
        }

        var expiryThreshold = today.AddDays(30);
        return filter.Status switch
        {
            DocumentListStatus.Draft => query.Where(document => document.State == LifecycleState.Draft),
            DocumentListStatus.Active => query.Where(document =>
                document.State == LifecycleState.Active &&
                (document.ExpiryDate == null || document.ExpiryDate > expiryThreshold)),
            DocumentListStatus.ExpiringSoon => query.Where(document =>
                document.State == LifecycleState.Active &&
                document.ExpiryDate >= today &&
                document.ExpiryDate <= expiryThreshold),
            DocumentListStatus.Expired => query.Where(document =>
                document.State == LifecycleState.Active &&
                document.ExpiryDate < today),
            DocumentListStatus.Archived => query.Where(document => document.State == LifecycleState.Archived),
            _ => query,
        };
    }
}
