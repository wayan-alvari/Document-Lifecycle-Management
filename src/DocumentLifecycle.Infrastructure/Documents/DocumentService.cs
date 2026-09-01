using System.Text.Json;
using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Application.Documents;
using DocumentLifecycle.Domain.Activity;
using DocumentLifecycle.Domain.Common;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Infrastructure.Documents;

internal sealed class DocumentService(
    ApplicationDbContext database,
    ICurrentWorkspace currentWorkspace,
    IClock clock) : IDocumentService
{
    public async Task<DocumentListPage> GetListAsync(
        DocumentListFilter filter,
        bool includeDrafts,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var query = DocumentQuery.ApplyFilters(
            database.ManagedDocuments.AsNoTracking(),
            filter,
            includeDrafts,
            today);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);
        var rows = await query
            .OrderByDescending(document => document.UpdatedAtUtc)
            .ThenBy(document => document.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(document => new DocumentRow(
                document.PublicId,
                document.Code,
                document.Title,
                document.Category.Name,
                document.Owner.DisplayName,
                document.EffectiveDate,
                document.ExpiryDate,
                document.State,
                document.Revisions.Count,
                document.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(row => new DocumentListItem(
                row.PublicId,
                row.Code,
                row.Title,
                row.Category,
                row.Owner,
                row.EffectiveDate,
                row.ExpiryDate,
                row.State,
                GetDisplayStatus(row.State, row.ExpiryDate, today),
                row.RevisionCount,
                row.UpdatedAtUtc))
            .ToList();

        return new DocumentListPage(items, totalCount, page, pageSize);
    }

    public async Task<DocumentFormOptions> GetFormOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await database.DocumentCategories
            .AsNoTracking()
            .OrderByDescending(category => category.IsActive)
            .ThenBy(category => category.Name)
            .Select(category => new DocumentReferenceOption(
                category.PublicId,
                category.Name,
                category.IsActive))
            .ToListAsync(cancellationToken);
        var owners = await database.DocumentOwners
            .AsNoTracking()
            .OrderByDescending(owner => owner.IsActive)
            .ThenBy(owner => owner.DisplayName)
            .Select(owner => new DocumentReferenceOption(
                owner.PublicId,
                owner.DisplayName,
                owner.IsActive))
            .ToListAsync(cancellationToken);
        return new DocumentFormOptions(categories, owners);
    }

    public async Task<DocumentDraftDetails?> GetDraftAsync(
        Guid publicId,
        CancellationToken cancellationToken = default) =>
        await database.ManagedDocuments
            .AsNoTracking()
            .Where(document =>
                document.PublicId == publicId &&
                document.State == LifecycleState.Draft)
            .Select(document => new DocumentDraftDetails(
                document.PublicId,
                document.Code,
                document.Title,
                document.Description,
                document.Category.PublicId,
                document.Owner.PublicId,
                document.EffectiveDate,
                document.ExpiryDate))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<DocumentDetails?> GetDetailsAsync(
        Guid publicId,
        bool includeDrafts,
        CancellationToken cancellationToken = default)
    {
        var query = database.ManagedDocuments
            .AsNoTracking()
            .Include(document => document.Category)
            .Include(document => document.Owner)
            .Include(document => document.Revisions)
            .Where(document => document.PublicId == publicId);
        if (!includeDrafts)
        {
            query = query.Where(document => document.State != LifecycleState.Draft);
        }

        var document = await query.SingleOrDefaultAsync(cancellationToken);
        if (document is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(clock.UtcNow);
        var auditTrail = await database.AuditEvents
            .AsNoTracking()
            .Where(audit =>
                audit.EntityType == nameof(ManagedDocument) &&
                audit.EntityPublicId == document.PublicId)
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .ThenByDescending(audit => audit.Id)
            .Take(25)
            .Select(audit => new DocumentAuditItem(
                audit.Actor,
                audit.Action,
                audit.OccurredAtUtc))
            .ToListAsync(cancellationToken);
        return new DocumentDetails(
            document.PublicId,
            document.Code,
            document.Title,
            document.Description,
            document.Category.Name,
            document.Owner.DisplayName,
            document.EffectiveDate,
            document.ExpiryDate,
            document.State,
            document.GetDisplayStatus(today),
            document.CreatedBy,
            document.CreatedAtUtc,
            document.UpdatedBy,
            document.UpdatedAtUtc,
            document.ArchiveReason,
            document.ArchivedBy,
            document.ArchivedAtUtc,
            document.Revisions
                .OrderByDescending(revision => revision.RevisionNumber)
                .Select(revision => new DocumentRevisionItem(
                    revision.PublicId,
                    revision.RevisionNumber,
                    revision.ChangeNote,
                    revision.OriginalFilename,
                    revision.MediaType,
                    revision.Size,
                    revision.UploadedBy,
                    revision.UploadedAtUtc))
                .ToList(),
            auditTrail);
    }

    public async Task<DocumentMutationResult> CreateDraftAsync(
        DocumentDraftInput input,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var references = await ResolveReferencesAsync(
            input.CategoryId,
            input.OwnerId,
            requireActive: true,
            cancellationToken);
        if (references is null)
        {
            return DocumentMutationResult.Reject("Choose an active category and owner.");
        }

        var workspaceId = currentWorkspace.WorkspaceId ??
            throw new InvalidOperationException("A current workspace is required.");
        var now = clock.UtcNow;
        var code = $"DOC-{now:yyyy}-{Guid.NewGuid():N}"[..17].ToUpperInvariant();
        var document = ManagedDocument.CreateDraft(
            workspaceId,
            code,
            input.Title,
            input.Description,
            references.Value.Category.Id,
            references.Value.Owner.Id,
            input.EffectiveDate,
            input.ExpiryDate,
            actor,
            now);
        database.ManagedDocuments.Add(document);
        AddAudit(document, actor, "Created", now);
        await database.SaveChangesAsync(cancellationToken);
        return DocumentMutationResult.Success(document.PublicId, $"Draft {document.Code} created.");
    }

    public async Task<DocumentMutationResult> UpdateDraftAsync(
        Guid publicId,
        DocumentDraftInput input,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var document = await database.ManagedDocuments.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (document is null)
        {
            return DocumentMutationResult.Missing();
        }

        if (document.State != LifecycleState.Draft)
        {
            return DocumentMutationResult.Reject("Only draft document metadata can be edited.");
        }

        var references = await ResolveReferencesAsync(
            input.CategoryId,
            input.OwnerId,
            requireActive: false,
            cancellationToken);
        if (references is null ||
            (!references.Value.Category.IsActive && references.Value.Category.Id != document.CategoryId) ||
            (!references.Value.Owner.IsActive && references.Value.Owner.Id != document.OwnerId))
        {
            return DocumentMutationResult.Reject("Choose an active category and owner.");
        }

        try
        {
            document.UpdateDraftMetadata(
                input.Title,
                input.Description,
                references.Value.Category.Id,
                references.Value.Owner.Id,
                input.EffectiveDate,
                input.ExpiryDate,
                actor,
                clock.UtcNow);
        }
        catch (DomainRuleException exception)
        {
            return DocumentMutationResult.Reject(exception.Message);
        }

        AddAudit(document, actor, "DraftUpdated", clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);
        return DocumentMutationResult.Success(document.PublicId, $"Draft {document.Code} updated.");
    }

    public async Task<DocumentMutationResult> ActivateAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var document = await database.ManagedDocuments
            .Include(item => item.Revisions)
            .SingleOrDefaultAsync(item => item.PublicId == publicId, cancellationToken);
        if (document is null)
        {
            return DocumentMutationResult.Missing();
        }

        var now = clock.UtcNow;
        try
        {
            document.Activate(actor, now);
        }
        catch (DomainRuleException exception)
        {
            return DocumentMutationResult.Reject(exception.Message);
        }

        AddAudit(document, actor, "Activated", now);
        await database.SaveChangesAsync(cancellationToken);
        return DocumentMutationResult.Success(document.PublicId, $"{document.Code} activated.");
    }

    public async Task<DocumentMutationResult> ArchiveAsync(
        Guid publicId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var document = await database.ManagedDocuments.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (document is null)
        {
            return DocumentMutationResult.Missing();
        }

        var now = clock.UtcNow;
        try
        {
            document.Archive(reason, actor, now);
        }
        catch (Exception exception) when (exception is DomainRuleException or ArgumentException)
        {
            return DocumentMutationResult.Reject(exception.Message);
        }

        database.AuditEvents.Add(AuditEvent.Create(
            document.WorkspaceId,
            actor,
            "Archived",
            nameof(ManagedDocument),
            document.PublicId,
            now,
            JsonSerializer.Serialize(new
            {
                document.Code,
                document.Title,
                Reason = document.ArchiveReason,
            })));
        await database.SaveChangesAsync(cancellationToken);
        return DocumentMutationResult.Success(document.PublicId, $"{document.Code} archived.");
    }

    public async Task<DocumentMutationResult> RestoreAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var document = await database.ManagedDocuments.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (document is null)
        {
            return DocumentMutationResult.Missing();
        }

        var now = clock.UtcNow;
        try
        {
            document.Restore(actor, now);
        }
        catch (DomainRuleException exception)
        {
            return DocumentMutationResult.Reject(exception.Message);
        }

        AddAudit(document, actor, "Restored", now);
        await database.SaveChangesAsync(cancellationToken);
        return DocumentMutationResult.Success(document.PublicId, $"{document.Code} restored to active circulation.");
    }

    private async Task<(DocumentCategory Category, DocumentOwner Owner)?> ResolveReferencesAsync(
        Guid categoryPublicId,
        Guid ownerPublicId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        var category = await database.DocumentCategories.SingleOrDefaultAsync(
            item => item.PublicId == categoryPublicId && (!requireActive || item.IsActive),
            cancellationToken);
        var owner = await database.DocumentOwners.SingleOrDefaultAsync(
            item => item.PublicId == ownerPublicId && (!requireActive || item.IsActive),
            cancellationToken);
        return category is null || owner is null ? null : (category, owner);
    }

    private void AddAudit(ManagedDocument document, string actor, string action, DateTime occurredAtUtc)
    {
        database.AuditEvents.Add(AuditEvent.Create(
            document.WorkspaceId,
            actor,
            action,
            nameof(ManagedDocument),
            document.PublicId,
            occurredAtUtc,
            JsonSerializer.Serialize(new
            {
                document.Code,
                document.Title,
            })));
    }

    private static DocumentDisplayStatus GetDisplayStatus(
        LifecycleState state,
        DateOnly? expiryDate,
        DateOnly today)
    {
        if (state == LifecycleState.Draft)
        {
            return DocumentDisplayStatus.Draft;
        }

        if (state == LifecycleState.Archived)
        {
            return DocumentDisplayStatus.Archived;
        }

        if (expiryDate is null)
        {
            return DocumentDisplayStatus.Active;
        }

        if (expiryDate < today)
        {
            return DocumentDisplayStatus.Expired;
        }

        return expiryDate <= today.AddDays(30)
            ? DocumentDisplayStatus.ExpiringSoon
            : DocumentDisplayStatus.Active;
    }

    private sealed record DocumentRow(
        Guid PublicId,
        string Code,
        string Title,
        string Category,
        string Owner,
        DateOnly EffectiveDate,
        DateOnly? ExpiryDate,
        LifecycleState State,
        int RevisionCount,
        DateTime UpdatedAtUtc);
}
