using DocumentLifecycle.Domain.Documents;

namespace DocumentLifecycle.Application.Documents;

public enum DocumentListStatus
{
    All,
    Draft,
    Active,
    ExpiringSoon,
    Expired,
    Archived,
}

public sealed record DocumentListFilter(
    string? Search,
    DocumentListStatus Status,
    Guid? CategoryId,
    Guid? OwnerId,
    DateOnly? ExpiryFrom,
    DateOnly? ExpiryTo,
    int Page = 1,
    int PageSize = 10);

public sealed record DocumentListPage(
    IReadOnlyList<DocumentListItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record DocumentListItem(
    Guid PublicId,
    string Code,
    string Title,
    string Category,
    string Owner,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate,
    LifecycleState State,
    DocumentDisplayStatus DisplayStatus,
    int RevisionCount,
    DateTime UpdatedAtUtc);

public sealed record DocumentReferenceOption(
    Guid PublicId,
    string Label,
    bool IsActive);

public sealed record DocumentFormOptions(
    IReadOnlyList<DocumentReferenceOption> Categories,
    IReadOnlyList<DocumentReferenceOption> Owners);

public sealed record DocumentDraftInput(
    string Title,
    string Description,
    Guid CategoryId,
    Guid OwnerId,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate);

public sealed record DocumentDraftDetails(
    Guid PublicId,
    string Code,
    string Title,
    string Description,
    Guid CategoryId,
    Guid OwnerId,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate);

public sealed record DocumentDetails(
    Guid PublicId,
    string Code,
    string Title,
    string Description,
    string Category,
    string Owner,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate,
    LifecycleState State,
    DocumentDisplayStatus DisplayStatus,
    string CreatedBy,
    DateTime CreatedAtUtc,
    string UpdatedBy,
    DateTime UpdatedAtUtc,
    string? ArchiveReason,
    string? ArchivedBy,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<DocumentRevisionItem> Revisions);

public sealed record DocumentRevisionItem(
    Guid PublicId,
    int RevisionNumber,
    string ChangeNote,
    string OriginalFilename,
    string MediaType,
    long Size,
    string UploadedBy,
    DateTime UploadedAtUtc);

public enum DocumentMutationStatus
{
    Succeeded,
    NotFound,
    Rejected,
}

public sealed record DocumentMutationResult(
    DocumentMutationStatus Status,
    Guid? PublicId = null,
    string? Message = null)
{
    public static DocumentMutationResult Success(Guid? publicId = null, string? message = null) =>
        new(DocumentMutationStatus.Succeeded, publicId, message);

    public static DocumentMutationResult Missing() =>
        new(DocumentMutationStatus.NotFound);

    public static DocumentMutationResult Reject(string message) =>
        new(DocumentMutationStatus.Rejected, Message: message);
}
