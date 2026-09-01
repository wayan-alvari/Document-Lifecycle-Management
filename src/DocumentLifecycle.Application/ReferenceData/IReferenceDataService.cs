namespace DocumentLifecycle.Application.ReferenceData;

public interface IReferenceDataService
{
    Task<IReadOnlyList<CategoryListItem>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<CategoryDetails?> GetCategoryAsync(Guid publicId, CancellationToken cancellationToken = default);

    Task<ReferenceMutationResult> CreateCategoryAsync(
        string name,
        string description,
        string actor,
        CancellationToken cancellationToken = default);

    Task<ReferenceMutationResult> UpdateCategoryAsync(
        Guid publicId,
        string name,
        string description,
        string actor,
        CancellationToken cancellationToken = default);

    Task<ReferenceMutationResult> ToggleCategoryAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default);

    Task<ReferenceMutationResult> DeleteCategoryAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OwnerListItem>> GetOwnersAsync(CancellationToken cancellationToken = default);

    Task<OwnerDetails?> GetOwnerAsync(Guid publicId, CancellationToken cancellationToken = default);

    Task<ReferenceMutationResult> CreateOwnerAsync(
        string displayName,
        string contact,
        string actor,
        CancellationToken cancellationToken = default);

    Task<ReferenceMutationResult> UpdateOwnerAsync(
        Guid publicId,
        string displayName,
        string contact,
        string actor,
        CancellationToken cancellationToken = default);

    Task<ReferenceMutationResult> ToggleOwnerAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default);

    Task<ReferenceMutationResult> DeleteOwnerAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default);
}
