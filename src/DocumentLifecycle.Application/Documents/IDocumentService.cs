namespace DocumentLifecycle.Application.Documents;

public interface IDocumentService
{
    Task<DocumentListPage> GetListAsync(
        DocumentListFilter filter,
        bool includeDrafts,
        CancellationToken cancellationToken = default);

    Task<DocumentFormOptions> GetFormOptionsAsync(CancellationToken cancellationToken = default);

    Task<DocumentDraftDetails?> GetDraftAsync(
        Guid publicId,
        CancellationToken cancellationToken = default);

    Task<DocumentDetails?> GetDetailsAsync(
        Guid publicId,
        bool includeDrafts,
        CancellationToken cancellationToken = default);

    Task<DocumentMutationResult> CreateDraftAsync(
        DocumentDraftInput input,
        string actor,
        CancellationToken cancellationToken = default);

    Task<DocumentMutationResult> UpdateDraftAsync(
        Guid publicId,
        DocumentDraftInput input,
        string actor,
        CancellationToken cancellationToken = default);

    Task<DocumentMutationResult> ActivateAsync(
        Guid publicId,
        string actor,
        CancellationToken cancellationToken = default);
}
