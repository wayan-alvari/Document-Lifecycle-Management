namespace DocumentLifecycle.Application.Files;

public interface IDocumentFileService
{
    Task<DocumentFileMutationResult> UploadRevisionAsync(
        Guid documentPublicId,
        RevisionUploadInput upload,
        string actor,
        CancellationToken cancellationToken = default);

    Task<DocumentDownload?> GetDownloadAsync(
        Guid documentPublicId,
        Guid revisionPublicId,
        bool allowDraft,
        CancellationToken cancellationToken = default);
}

public sealed record RevisionUploadInput(
    string ChangeNote,
    string OriginalFilename,
    string DeclaredMediaType,
    long DeclaredLength,
    Stream Content);

public sealed record DocumentDownload(
    Stream Content,
    string MediaType,
    string DownloadFilename,
    long Size,
    string Sha256Hash);

public enum DocumentFileMutationStatus
{
    Succeeded,
    NotFound,
    Rejected,
}

public sealed record DocumentFileMutationResult(
    DocumentFileMutationStatus Status,
    string? Message = null)
{
    public static DocumentFileMutationResult Success(string message) =>
        new(DocumentFileMutationStatus.Succeeded, message);

    public static DocumentFileMutationResult Missing() =>
        new(DocumentFileMutationStatus.NotFound);

    public static DocumentFileMutationResult Reject(string message) =>
        new(DocumentFileMutationStatus.Rejected, message);
}
