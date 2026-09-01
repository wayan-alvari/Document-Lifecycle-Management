using ClosedXML.Excel;
using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Documents;
using DocumentLifecycle.Application.Reports;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Fonts;

namespace DocumentLifecycle.Infrastructure.Reports;

internal sealed class DocumentReportService(
    ApplicationDbContext database,
    IDocumentService documents,
    IClock clock) : IDocumentReportService
{
    private const string SpreadsheetContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";
    private static readonly object FontConfigurationLock = new();
    private static bool fontsConfigured;

    public async Task<GeneratedReport> ExportListAsync(
        DocumentListFilter filter,
        bool includeDrafts,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var rows = await DocumentQuery.ApplyFilters(
                database.ManagedDocuments.AsNoTracking(),
                filter,
                includeDrafts,
                today)
            .OrderByDescending(document => document.UpdatedAtUtc)
            .ThenBy(document => document.Code)
            .Select(document => new ReportRow(
                document.Code,
                document.Title,
                document.Category.Name,
                document.Owner.DisplayName,
                document.State,
                document.EffectiveDate,
                document.ExpiryDate,
                document.Revisions.Count,
                document.UpdatedBy,
                document.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Documents");
        worksheet.Cell(1, 1).Value = "Document lifecycle register";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Range(1, 1, 1, 10).Merge();
        worksheet.Cell(2, 1).Value = $"Generated {clock.UtcNow:dd MMM yyyy, HH:mm 'UTC'}";
        worksheet.Range(2, 1, 2, 10).Merge();
        worksheet.Cell(3, 1).Value = "Browser-isolated demo data. Current document filters are applied.";
        worksheet.Range(3, 1, 3, 10).Merge();

        var headers = new[]
        {
            "Code",
            "Title",
            "Category",
            "Owner",
            "Status",
            "Effective date",
            "Review date",
            "Revisions",
            "Updated by",
            "Updated UTC",
        };
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(5, column + 1).Value = headers[column];
        }

        var headerRange = worksheet.Range(5, 1, 5, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#16324F");

        for (var index = 0; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowNumber = index + 6;
            var row = rows[index];
            worksheet.Cell(rowNumber, 1).Value = SafeSpreadsheetText(row.Code);
            worksheet.Cell(rowNumber, 2).Value = SafeSpreadsheetText(row.Title);
            worksheet.Cell(rowNumber, 3).Value = SafeSpreadsheetText(row.Category);
            worksheet.Cell(rowNumber, 4).Value = SafeSpreadsheetText(row.Owner);
            worksheet.Cell(rowNumber, 5).Value = DisplayStatus(row.State, row.ExpiryDate, today);
            worksheet.Cell(rowNumber, 6).Value = row.EffectiveDate.ToDateTime(TimeOnly.MinValue);
            if (row.ExpiryDate is not null)
            {
                worksheet.Cell(rowNumber, 7).Value = row.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue);
            }

            worksheet.Cell(rowNumber, 8).Value = row.RevisionCount;
            worksheet.Cell(rowNumber, 9).Value = SafeSpreadsheetText(row.UpdatedBy);
            worksheet.Cell(rowNumber, 10).Value = row.UpdatedAtUtc;
        }

        var lastRow = Math.Max(6, rows.Count + 5);
        worksheet.Range(6, 6, lastRow, 7).Style.DateFormat.Format = "dd mmm yyyy";
        worksheet.Range(6, 10, lastRow, 10).Style.DateFormat.Format = "dd mmm yyyy hh:mm";
        worksheet.Range(5, 1, Math.Max(5, rows.Count + 5), headers.Length).SetAutoFilter();
        worksheet.SheetView.FreezeRows(5);
        worksheet.Columns(1, headers.Length).AdjustToContents();
        foreach (var column in worksheet.ColumnsUsed())
        {
            column.Width = Math.Min(column.Width, 42);
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return new GeneratedReport(
            output.ToArray(),
            SpreadsheetContentType,
            $"document-lifecycle-{clock.UtcNow:yyyyMMdd}.xlsx");
    }

    public async Task<GeneratedReport?> CreateSummaryAsync(
        Guid publicId,
        bool includeDrafts,
        CancellationToken cancellationToken = default)
    {
        var source = await documents.GetDetailsAsync(publicId, includeDrafts, cancellationToken);
        if (source is null)
        {
            return null;
        }

        EnsureFontsConfigured();
        var summary = CreatePdfDocument(source);
        var renderer = new PdfDocumentRenderer
        {
            Document = summary,
        };
        renderer.RenderDocument();

        using var output = new MemoryStream();
        renderer.Save(output, closeStream: false);
        return new GeneratedReport(
            output.ToArray(),
            PdfContentType,
            $"{source.Code.ToLowerInvariant()}-summary.pdf");
    }

    private MigraDoc.DocumentObjectModel.Document CreatePdfDocument(DocumentDetails source)
    {
        var document = new MigraDoc.DocumentObjectModel.Document();
        document.Info.Title = $"{source.Code} - {source.Title}";
        document.Info.Subject = "Document lifecycle metadata and history summary";
        document.Info.Author = "Document Lifecycle Management portfolio demo";
        document.Info.Keywords =
            $"status:{StatusLabel(source.DisplayStatus)};revisions:{source.Revisions.Count};events:{source.AuditTrail.Count}";

        var normal = document.Styles[StyleNames.Normal] ??
            throw new InvalidOperationException("The PDF document is missing its normal style.");
        normal.Font.Name = "Arial";
        normal.Font.Size = Unit.FromPoint(9);
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.7);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.7);

        var title = section.AddParagraph("Document lifecycle summary");
        title.Format.Font.Size = Unit.FromPoint(18);
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color.FromRgb(22, 50, 79);
        title.Format.SpaceAfter = Unit.FromPoint(4);

        var identity = section.AddParagraph();
        identity.AddFormattedText(source.Code, TextFormat.Bold);
        identity.AddText($"  |  {StatusLabel(source.DisplayStatus)}");
        identity.Format.Font.Color = Color.FromRgb(61, 83, 102);

        var heading = section.AddParagraph(source.Title);
        heading.Format.Font.Size = Unit.FromPoint(14);
        heading.Format.Font.Bold = true;
        heading.Format.SpaceBefore = Unit.FromPoint(6);
        heading.Format.SpaceAfter = Unit.FromPoint(8);

        AddSectionHeading(section, "Metadata");
        AddField(section, "Description", source.Description);
        AddField(section, "Category", source.Category);
        AddField(section, "Owner", source.Owner);
        AddField(section, "Effective date", source.EffectiveDate.ToString("dd MMM yyyy"));
        AddField(section, "Review date", source.ExpiryDate?.ToString("dd MMM yyyy") ?? "Not scheduled");
        AddField(section, "Created", $"{source.CreatedAtUtc:dd MMM yyyy, HH:mm 'UTC'} by {source.CreatedBy}");
        AddField(section, "Last updated", $"{source.UpdatedAtUtc:dd MMM yyyy, HH:mm 'UTC'} by {source.UpdatedBy}");
        if (source.State == LifecycleState.Archived)
        {
            AddField(section, "Archive reason", source.ArchiveReason ?? "Not recorded");
            AddField(section, "Archived", $"{source.ArchivedAtUtc:dd MMM yyyy, HH:mm 'UTC'} by {source.ArchivedBy}");
        }

        AddSectionHeading(section, $"Revision history ({source.Revisions.Count})");
        if (source.Revisions.Count == 0)
        {
            section.AddParagraph("No revisions have been uploaded.");
        }
        else
        {
            foreach (var revision in source.Revisions)
            {
                var paragraph = section.AddParagraph();
                paragraph.AddFormattedText($"v{revision.RevisionNumber} - {revision.OriginalFilename}", TextFormat.Bold);
                paragraph.AddLineBreak();
                paragraph.AddText(revision.ChangeNote);
                paragraph.AddLineBreak();
                paragraph.AddText($"{revision.UploadedAtUtc:dd MMM yyyy, HH:mm 'UTC'} by {revision.UploadedBy}");
                paragraph.Format.LeftIndent = Unit.FromCentimeter(0.3);
                paragraph.Format.SpaceAfter = Unit.FromPoint(6);
            }
        }

        AddSectionHeading(section, $"Recent activity ({source.AuditTrail.Count})");
        foreach (var activity in source.AuditTrail)
        {
            var paragraph = section.AddParagraph();
            paragraph.AddFormattedText(Humanize(activity.Action), TextFormat.Bold);
            paragraph.AddText($" - {activity.OccurredAtUtc:dd MMM yyyy, HH:mm 'UTC'} by {activity.Actor}");
            paragraph.Format.LeftIndent = Unit.FromCentimeter(0.3);
        }

        var footer = section.Footers.Primary.AddParagraph(
            $"Generated {clock.UtcNow:dd MMM yyyy, HH:mm 'UTC'} | Metadata and history only; uploaded file content is not included.");
        footer.Format.Font.Size = Unit.FromPoint(7);
        footer.Format.Font.Color = Color.FromRgb(91, 107, 122);
        footer.Format.Alignment = ParagraphAlignment.Center;
        return document;
    }

    private static void AddSectionHeading(Section section, string text)
    {
        var paragraph = section.AddParagraph(text);
        paragraph.Format.Font.Size = Unit.FromPoint(11);
        paragraph.Format.Font.Bold = true;
        paragraph.Format.Font.Color = Color.FromRgb(22, 50, 79);
        paragraph.Format.SpaceBefore = Unit.FromPoint(10);
        paragraph.Format.SpaceAfter = Unit.FromPoint(5);
        paragraph.Format.KeepWithNext = true;
    }

    private static void AddField(Section section, string label, string value)
    {
        var paragraph = section.AddParagraph();
        paragraph.AddFormattedText($"{label}: ", TextFormat.Bold);
        paragraph.AddText(value);
    }

    private static string SafeSpreadsheetText(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@'
            ? $"'{value}"
            : value;

    private static string DisplayStatus(
        LifecycleState state,
        DateOnly? expiryDate,
        DateOnly today) => StatusLabel(state switch
        {
            LifecycleState.Draft => DocumentDisplayStatus.Draft,
            LifecycleState.Archived => DocumentDisplayStatus.Archived,
            _ when expiryDate is not null && expiryDate < today => DocumentDisplayStatus.Expired,
            _ when expiryDate is not null && expiryDate <= today.AddDays(30) => DocumentDisplayStatus.ExpiringSoon,
            _ => DocumentDisplayStatus.Active,
        });

    private static string StatusLabel(DocumentDisplayStatus status) => status == DocumentDisplayStatus.ExpiringSoon
        ? "Expiring soon"
        : status.ToString();

    private static string Humanize(string value) => value switch
    {
        "DraftUpdated" => "Draft updated",
        "RevisionUploaded" => "Revision uploaded",
        _ => value,
    };

    private static void EnsureFontsConfigured()
    {
        if (fontsConfigured)
        {
            return;
        }

        lock (FontConfigurationLock)
        {
            if (fontsConfigured)
            {
                return;
            }

            GlobalFontSettings.FontResolver = SystemSansFontResolver.Instance;
            fontsConfigured = true;
        }
    }

    private sealed class SystemSansFontResolver : IFontResolver
    {
        private const string FaceName = "system-sans-regular";
        private readonly Lazy<byte[]> fontData = new(LoadFontData);

        private SystemSansFontResolver()
        {
        }

        public static SystemSansFontResolver Instance { get; } = new();

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(FaceName, isBold, isItalic);

        public byte[] GetFont(string faceName) => faceName == FaceName
            ? fontData.Value
            : throw new InvalidOperationException($"Unknown PDF font face '{faceName}'.");

        private static byte[] LoadFontData()
        {
            var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var candidates = new[]
            {
                Path.Combine(fontDirectory, "arial.ttf"),
                Path.Combine(fontDirectory, "segoeui.ttf"),
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
            };
            var fontPath = candidates.FirstOrDefault(File.Exists) ??
                throw new InvalidOperationException("A system sans-serif font is required to generate PDF summaries.");
            return File.ReadAllBytes(fontPath);
        }
    }

    private sealed record ReportRow(
        string Code,
        string Title,
        string Category,
        string Owner,
        LifecycleState State,
        DateOnly EffectiveDate,
        DateOnly? ExpiryDate,
        int RevisionCount,
        string UpdatedBy,
        DateTime UpdatedAtUtc);
}
