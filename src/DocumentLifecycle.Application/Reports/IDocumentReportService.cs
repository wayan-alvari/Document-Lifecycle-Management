using DocumentLifecycle.Application.Documents;

namespace DocumentLifecycle.Application.Reports;

public interface IDocumentReportService
{
    Task<GeneratedReport> ExportListAsync(
        DocumentListFilter filter,
        bool includeDrafts,
        CancellationToken cancellationToken = default);

    Task<GeneratedReport?> CreateSummaryAsync(
        Guid publicId,
        bool includeDrafts,
        CancellationToken cancellationToken = default);
}

public sealed record GeneratedReport(
    byte[] Content,
    string ContentType,
    string DownloadFilename);
