using DocumentLifecycle.Domain.Common;

namespace DocumentLifecycle.Domain.Documents;

public sealed class ManagedDocument : IWorkspaceScoped
{
    private readonly List<DocumentRevision> revisions = [];

    private ManagedDocument()
    {
    }

    private ManagedDocument(
        Guid workspaceId,
        string code,
        string title,
        string description,
        long categoryId,
        long ownerId,
        DateOnly effectiveDate,
        DateOnly? expiryDate,
        string actor,
        DateTime createdAtUtc)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(workspaceId));
        }

        if (categoryId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(categoryId));
        }

        if (ownerId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerId));
        }

        if (expiryDate is not null && expiryDate < effectiveDate)
        {
            throw new ArgumentException("Expiry date cannot precede the effective date.", nameof(expiryDate));
        }

        WorkspaceId = workspaceId;
        PublicId = Guid.NewGuid();
        Code = Required(code, nameof(code), 40);
        Title = Required(title, nameof(title), 200);
        Description = Optional(description, 2000);
        CategoryId = categoryId;
        OwnerId = ownerId;
        EffectiveDate = effectiveDate;
        ExpiryDate = expiryDate;
        State = LifecycleState.Draft;
        CreatedBy = Required(actor, nameof(actor), 256);
        UpdatedBy = CreatedBy;
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
    }

    public long Id { get; private set; }

    public Guid PublicId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public long CategoryId { get; private set; }

    public DocumentCategory Category { get; private set; } = null!;

    public long OwnerId { get; private set; }

    public DocumentOwner Owner { get; private set; } = null!;

    public DateOnly EffectiveDate { get; private set; }

    public DateOnly? ExpiryDate { get; private set; }

    public LifecycleState State { get; private set; }

    public string? ArchiveReason { get; private set; }

    public string? ArchivedBy { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<DocumentRevision> Revisions => revisions.AsReadOnly();

    public static ManagedDocument CreateDraft(
        Guid workspaceId,
        string code,
        string title,
        string description,
        long categoryId,
        long ownerId,
        DateOnly effectiveDate,
        DateOnly? expiryDate,
        string actor,
        DateTime createdAtUtc) =>
        new(
            workspaceId,
            code,
            title,
            description,
            categoryId,
            ownerId,
            effectiveDate,
            expiryDate,
            actor,
            createdAtUtc);

    public DocumentRevision AddRevision(
        string changeNote,
        string originalFilename,
        string storedFilename,
        string mediaType,
        long size,
        string sha256Hash,
        string actor,
        DateTime uploadedAtUtc)
    {
        if (State == LifecycleState.Archived)
        {
            throw new DomainRuleException("Archived documents cannot receive a revision.");
        }

        var revision = new DocumentRevision(
            WorkspaceId,
            revisions.Count == 0 ? 1 : revisions.Max(item => item.RevisionNumber) + 1,
            changeNote,
            originalFilename,
            storedFilename,
            mediaType,
            size,
            sha256Hash,
            actor,
            uploadedAtUtc);
        revisions.Add(revision);
        MarkUpdated(actor, uploadedAtUtc);
        return revision;
    }

    public void Activate(string actor, DateTime utcNow)
    {
        if (State != LifecycleState.Draft)
        {
            throw new DomainRuleException("Only draft documents can be activated.");
        }

        if (string.IsNullOrWhiteSpace(Title) || CategoryId < 1 || OwnerId < 1 || revisions.Count == 0)
        {
            throw new DomainRuleException(
                "Activation requires a title, category, owner, effective date, and at least one revision.");
        }

        State = LifecycleState.Active;
        MarkUpdated(actor, utcNow);
    }

    public void Archive(string reason, string actor, DateTime utcNow)
    {
        if (State != LifecycleState.Active)
        {
            throw new DomainRuleException("Only active documents can be archived.");
        }

        ArchiveReason = Required(reason, nameof(reason), 500);
        ArchivedBy = Required(actor, nameof(actor), 256);
        ArchivedAtUtc = EnsureUtc(utcNow);
        State = LifecycleState.Archived;
        MarkUpdated(actor, utcNow);
    }

    public void Restore(string actor, DateTime utcNow)
    {
        if (State != LifecycleState.Archived)
        {
            throw new DomainRuleException("Only archived documents can be restored.");
        }

        State = LifecycleState.Active;
        ArchiveReason = null;
        ArchivedBy = null;
        ArchivedAtUtc = null;
        MarkUpdated(actor, utcNow);
    }

    public DocumentDisplayStatus GetDisplayStatus(DateOnly today)
    {
        if (State == LifecycleState.Draft)
        {
            return DocumentDisplayStatus.Draft;
        }

        if (State == LifecycleState.Archived)
        {
            return DocumentDisplayStatus.Archived;
        }

        if (ExpiryDate is null)
        {
            return DocumentDisplayStatus.Active;
        }

        if (ExpiryDate < today)
        {
            return DocumentDisplayStatus.Expired;
        }

        return ExpiryDate <= today.AddDays(30)
            ? DocumentDisplayStatus.ExpiringSoon
            : DocumentDisplayStatus.Active;
    }

    private void MarkUpdated(string actor, DateTime utcNow)
    {
        UpdatedBy = Required(actor, nameof(actor), 256);
        UpdatedAtUtc = EnsureUtc(utcNow);
    }

    private static string Required(string value, string parameterName, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value must contain 1 to {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string Optional(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", nameof(value));
        }

        return normalized;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : throw new ArgumentException("Document timestamps must be UTC.", nameof(value));
}
