using System.Text.Json;
using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Application.ReferenceData;
using DocumentLifecycle.Domain.Activity;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Infrastructure.ReferenceData;

internal sealed class ReferenceDataService(
    ApplicationDbContext database,
    ICurrentWorkspace currentWorkspace,
    IClock clock) : IReferenceDataService
{
    public async Task<IReadOnlyList<CategoryListItem>> GetCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        await database.DocumentCategories
            .AsNoTracking()
            .OrderByDescending(category => category.IsActive)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryListItem(
                category.PublicId,
                category.Name,
                category.Description,
                category.IsActive,
                database.ManagedDocuments.Count(document => document.CategoryId == category.Id)))
            .ToListAsync(cancellationToken);

    public async Task<CategoryDetails?> GetCategoryAsync(
        Guid publicId,
        CancellationToken cancellationToken = default) =>
        await database.DocumentCategories
            .AsNoTracking()
            .Where(category => category.PublicId == publicId)
            .Select(category => new CategoryDetails(
                category.PublicId,
                category.Name,
                category.Description,
                category.IsActive,
                database.ManagedDocuments.Count(document => document.CategoryId == category.Id)))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ReferenceMutationResult> CreateCategoryAsync(
        string name,
        string description,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (await CategoryNameExistsAsync(name, null, cancellationToken))
        {
            return ReferenceMutationResult.Reject("A category with this name already exists.");
        }

        var workspaceId = GetWorkspaceId();
        var category = DocumentCategory.Create(workspaceId, name, description);
        database.DocumentCategories.Add(category);
        AddAudit(workspaceId, actor, "CategoryCreated", category.PublicId, category.Name);
        await database.SaveChangesAsync(cancellationToken);
        return ReferenceMutationResult.Success(category.PublicId, "Category created.");
    }

    public async Task<ReferenceMutationResult> UpdateCategoryAsync(
        Guid publicId,
        string name,
        string description,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var category = await database.DocumentCategories.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (category is null)
        {
            return ReferenceMutationResult.Missing();
        }

        if (await CategoryNameExistsAsync(name, category.Id, cancellationToken))
        {
            return ReferenceMutationResult.Reject("A category with this name already exists.");
        }

        category.Update(name, description);
        AddAudit(category.WorkspaceId, actor, "CategoryUpdated", category.PublicId, category.Name);
        await database.SaveChangesAsync(cancellationToken);
        return ReferenceMutationResult.Success(message: "Category updated.");
    }

    public async Task<ReferenceMutationResult> ToggleCategoryAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var category = await database.DocumentCategories.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (category is null)
        {
            return ReferenceMutationResult.Missing();
        }

        var action = category.IsActive ? "CategoryDeactivated" : "CategoryReactivated";
        if (category.IsActive)
        {
            category.Deactivate();
        }
        else
        {
            category.Reactivate();
        }

        AddAudit(category.WorkspaceId, actor, action, category.PublicId, category.Name);
        await database.SaveChangesAsync(cancellationToken);
        return ReferenceMutationResult.Success(
            message: category.IsActive ? "Category reactivated." : "Category deactivated.");
    }

    public async Task<ReferenceMutationResult> DeleteCategoryAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var category = await database.DocumentCategories.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (category is null)
        {
            return ReferenceMutationResult.Missing();
        }

        if (await database.ManagedDocuments.AnyAsync(
                document => document.CategoryId == category.Id,
                cancellationToken))
        {
            return ReferenceMutationResult.Reject(
                "This category is referenced by documents. Deactivate it instead of deleting it.");
        }

        database.DocumentCategories.Remove(category);
        AddAudit(category.WorkspaceId, actor, "CategoryDeleted", category.PublicId, category.Name);
        await database.SaveChangesAsync(cancellationToken);
        return ReferenceMutationResult.Success(message: "Category deleted.");
    }

    public async Task<IReadOnlyList<OwnerListItem>> GetOwnersAsync(
        CancellationToken cancellationToken = default) =>
        await database.DocumentOwners
            .AsNoTracking()
            .OrderByDescending(owner => owner.IsActive)
            .ThenBy(owner => owner.DisplayName)
            .Select(owner => new OwnerListItem(
                owner.PublicId,
                owner.DisplayName,
                owner.Contact,
                owner.IsActive,
                database.ManagedDocuments.Count(document => document.OwnerId == owner.Id)))
            .ToListAsync(cancellationToken);

    public async Task<OwnerDetails?> GetOwnerAsync(
        Guid publicId,
        CancellationToken cancellationToken = default) =>
        await database.DocumentOwners
            .AsNoTracking()
            .Where(owner => owner.PublicId == publicId)
            .Select(owner => new OwnerDetails(
                owner.PublicId,
                owner.DisplayName,
                owner.Contact,
                owner.IsActive,
                database.ManagedDocuments.Count(document => document.OwnerId == owner.Id)))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ReferenceMutationResult> CreateOwnerAsync(
        string displayName,
        string contact,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (await OwnerNameExistsAsync(displayName, null, cancellationToken))
        {
            return ReferenceMutationResult.Reject("An owner with this display name already exists.");
        }

        var workspaceId = GetWorkspaceId();
        var owner = DocumentOwner.Create(workspaceId, displayName, contact);
        database.DocumentOwners.Add(owner);
        AddAudit(workspaceId, actor, "OwnerCreated", owner.PublicId, owner.DisplayName);
        await database.SaveChangesAsync(cancellationToken);
        return ReferenceMutationResult.Success(owner.PublicId, "Owner created.");
    }

    public async Task<ReferenceMutationResult> UpdateOwnerAsync(
        Guid publicId,
        string displayName,
        string contact,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var owner = await database.DocumentOwners.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (owner is null)
        {
            return ReferenceMutationResult.Missing();
        }

        if (await OwnerNameExistsAsync(displayName, owner.Id, cancellationToken))
        {
            return ReferenceMutationResult.Reject("An owner with this display name already exists.");
        }

        owner.Update(displayName, contact);
        AddAudit(owner.WorkspaceId, actor, "OwnerUpdated", owner.PublicId, owner.DisplayName);
        await database.SaveChangesAsync(cancellationToken);
        return ReferenceMutationResult.Success(message: "Owner updated.");
    }

    public async Task<ReferenceMutationResult> ToggleOwnerAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var owner = await database.DocumentOwners.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (owner is null)
        {
            return ReferenceMutationResult.Missing();
        }

        var action = owner.IsActive ? "OwnerDeactivated" : "OwnerReactivated";
        if (owner.IsActive)
        {
            owner.Deactivate();
        }
        else
        {
            owner.Reactivate();
        }

        AddAudit(owner.WorkspaceId, actor, action, owner.PublicId, owner.DisplayName);
        await database.SaveChangesAsync(cancellationToken);
        return ReferenceMutationResult.Success(
            message: owner.IsActive ? "Owner reactivated." : "Owner deactivated.");
    }

    public async Task<ReferenceMutationResult> DeleteOwnerAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var owner = await database.DocumentOwners.SingleOrDefaultAsync(
            item => item.PublicId == publicId,
            cancellationToken);
        if (owner is null)
        {
            return ReferenceMutationResult.Missing();
        }

        if (await database.ManagedDocuments.AnyAsync(
                document => document.OwnerId == owner.Id,
                cancellationToken))
        {
            return ReferenceMutationResult.Reject(
                "This owner is referenced by documents. Deactivate it instead of deleting it.");
        }

        database.DocumentOwners.Remove(owner);
        AddAudit(owner.WorkspaceId, actor, "OwnerDeleted", owner.PublicId, owner.DisplayName);
        await database.SaveChangesAsync(cancellationToken);
        return ReferenceMutationResult.Success(message: "Owner deleted.");
    }

    private async Task<bool> CategoryNameExistsAsync(
        string name,
        long? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToUpper();
        return await database.DocumentCategories.AnyAsync(
            category => category.Id != excludingId && category.Name.ToUpper() == normalized,
            cancellationToken);
    }

    private async Task<bool> OwnerNameExistsAsync(
        string displayName,
        long? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = displayName.Trim().ToUpper();
        return await database.DocumentOwners.AnyAsync(
            owner => owner.Id != excludingId && owner.DisplayName.ToUpper() == normalized,
            cancellationToken);
    }

    private Guid GetWorkspaceId() => currentWorkspace.WorkspaceId ??
        throw new InvalidOperationException("A current workspace is required.");

    private void AddAudit(
        Guid workspaceId,
        string actor,
        string action,
        Guid publicId,
        string label)
    {
        database.AuditEvents.Add(AuditEvent.Create(
            workspaceId,
            actor,
            action,
            "ReferenceData",
            publicId,
            clock.UtcNow,
            JsonSerializer.Serialize(new { Label = label })));
    }
}
