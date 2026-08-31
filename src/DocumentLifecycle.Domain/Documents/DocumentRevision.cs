using DocumentLifecycle.Domain.Common;

namespace DocumentLifecycle.Domain.Documents;

public sealed class DocumentRevision : IWorkspaceScoped
{
    private DocumentRevision()
    {
    }

    internal DocumentRevision(
        Guid workspaceId,
        int revisionNumber,
        string changeNote,
        string originalFilename,
        string storedFilename,
        string mediaType,
        long size,
        string sha256Hash,
        string uploadedBy,
        DateTime uploadedAtUtc)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(workspaceId));
        }

        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        }

        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (sha256Hash.Length != 64 || !sha256Hash.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A 64-character SHA-256 hash is required.", nameof(sha256Hash));
        }

        WorkspaceId = workspaceId;
        PublicId = Guid.NewGuid();
        RevisionNumber = revisionNumber;
        ChangeNote = Required(changeNote, nameof(changeNote), 500);
        OriginalFilename = Required(originalFilename, nameof(originalFilename), 255);
        StoredFilename = Required(storedFilename, nameof(storedFilename), 80);
        MediaType = Required(mediaType, nameof(mediaType), 100);
        Size = size;
        Sha256Hash = sha256Hash.ToLowerInvariant();
        UploadedBy = Required(uploadedBy, nameof(uploadedBy), 256);
        UploadedAtUtc = EnsureUtc(uploadedAtUtc);
    }

    public long Id { get; private set; }

    public Guid PublicId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public long ManagedDocumentId { get; private set; }

    public int RevisionNumber { get; private set; }

    public string ChangeNote { get; private set; } = string.Empty;

    public string OriginalFilename { get; private set; } = string.Empty;

    public string StoredFilename { get; private set; } = string.Empty;

    public string MediaType { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public string Sha256Hash { get; private set; } = string.Empty;

    public string UploadedBy { get; private set; } = string.Empty;

    public DateTime UploadedAtUtc { get; private set; }

    private static string Required(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value must contain 1 to {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : throw new ArgumentException("Revision timestamps must be UTC.", nameof(value));
}
