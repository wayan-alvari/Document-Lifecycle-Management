namespace DocumentLifecycle.Application.ReferenceData;

public sealed record CategoryListItem(
    Guid PublicId,
    string Name,
    string Description,
    bool IsActive,
    int DocumentCount);

public sealed record CategoryDetails(
    Guid PublicId,
    string Name,
    string Description,
    bool IsActive,
    int DocumentCount);

public sealed record OwnerListItem(
    Guid PublicId,
    string DisplayName,
    string Contact,
    bool IsActive,
    int DocumentCount);

public sealed record OwnerDetails(
    Guid PublicId,
    string DisplayName,
    string Contact,
    bool IsActive,
    int DocumentCount);

public enum ReferenceMutationStatus
{
    Succeeded,
    NotFound,
    Rejected,
}

public sealed record ReferenceMutationResult(
    ReferenceMutationStatus Status,
    string? Message = null,
    Guid? PublicId = null)
{
    public static ReferenceMutationResult Success(Guid? publicId = null, string? message = null) =>
        new(ReferenceMutationStatus.Succeeded, message, publicId);

    public static ReferenceMutationResult Missing() =>
        new(ReferenceMutationStatus.NotFound);

    public static ReferenceMutationResult Reject(string message) =>
        new(ReferenceMutationStatus.Rejected, message);
}
