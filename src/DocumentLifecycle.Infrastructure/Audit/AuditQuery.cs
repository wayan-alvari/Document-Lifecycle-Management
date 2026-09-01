using System.Text.Json;
using DocumentLifecycle.Application.Audit;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Infrastructure.Audit;

internal sealed class AuditQuery(ApplicationDbContext database) : IAuditQuery
{
    public async Task<AuditListPage> GetAsync(
        AuditListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = database.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(audit =>
                audit.Actor.Contains(search) ||
                audit.Action.Contains(search) ||
                audit.EntityType.Contains(search) ||
                audit.DetailsJson.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(audit => audit.Action == filter.Action);
        }

        var availableActions = await database.AuditEvents
            .AsNoTracking()
            .Select(audit => audit.Action)
            .Distinct()
            .OrderBy(action => action)
            .ToListAsync(cancellationToken);
        var totalCount = await query.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);
        var rows = await query
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .ThenByDescending(audit => audit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(audit => new AuditRow(
                audit.PublicId,
                audit.Actor,
                audit.Action,
                audit.EntityType,
                audit.EntityPublicId,
                audit.OccurredAtUtc,
                audit.DetailsJson))
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(row => new AuditListItem(
                row.PublicId,
                row.Actor,
                row.Action,
                row.EntityType,
                row.EntityPublicId,
                row.OccurredAtUtc,
                Summarize(row.EntityType, row.DetailsJson)))
            .ToList();
        return new AuditListPage(items, availableActions, totalCount, page, pageSize);
    }

    private static string Summarize(string entityType, string detailsJson)
    {
        try
        {
            using var details = JsonDocument.Parse(detailsJson);
            var root = details.RootElement;
            var code = GetString(root, "Code");
            var title = GetString(root, "Title");
            var label = GetString(root, "Label");
            var reason = GetString(root, "Reason");
            var filename = GetString(root, "Filename");

            var identity = !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(title)
                ? $"{code} - {title}"
                : label ?? code ?? title ?? entityType;
            if (!string.IsNullOrWhiteSpace(filename))
            {
                identity = $"{identity}; file {filename}";
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                identity = $"{identity}; reason: {reason}";
            }

            return identity;
        }
        catch (JsonException)
        {
            return entityType;
        }
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private sealed record AuditRow(
        Guid PublicId,
        string Actor,
        string Action,
        string EntityType,
        Guid EntityPublicId,
        DateTime OccurredAtUtc,
        string DetailsJson);
}
