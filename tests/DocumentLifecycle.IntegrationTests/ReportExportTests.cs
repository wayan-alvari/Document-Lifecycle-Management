using System.Net;
using System.Text;
using ClosedXML.Excel;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.IO;

namespace DocumentLifecycle.IntegrationTests;

public sealed class ReportExportTests
{
    [Fact]
    public async Task XlsxExportHonorsFiltersAndIncludesAllMatchingRows()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        await SignInAsync(client, "manager@documents.demo");

        var allResponse = await client.GetAsync("/Documents/Export");
        Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            allResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("document-lifecycle-20260115.xlsx", DownloadFilename(allResponse));
        using var allWorkbook = new XLWorkbook(
            new MemoryStream(await allResponse.Content.ReadAsByteArrayAsync()));
        var allSheet = allWorkbook.Worksheet("Documents");
        Assert.Equal("Document lifecycle register", allSheet.Cell(1, 1).GetString());
        Assert.Equal(12, ExportedTitles(allSheet).Count);
        Assert.Contains("Remote Work Guide", ExportedTitles(allSheet));

        var filteredResponse = await client.GetAsync(
            "/Documents/Export?Status=ExpiringSoon&Search=Records");
        using var filteredWorkbook = new XLWorkbook(
            new MemoryStream(await filteredResponse.Content.ReadAsByteArrayAsync()));
        var filteredSheet = filteredWorkbook.Worksheet("Documents");
        var titles = ExportedTitles(filteredSheet);

        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.Equal(["Records Handling Policy"], titles);
        Assert.Equal("Expiring soon", filteredSheet.Cell(6, 5).GetString());
    }

    [Fact]
    public async Task ViewerExportSuppressesDraftsAndSpreadsheetFormulasAreNeutralized()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var manager = CreateClient(factory);
        var login = await SignInAsync(manager, "manager@documents.demo");
        var workspaceId = GetWorkspaceId(login);
        var (categoryId, ownerId) = await GetReferenceIdsAsync(factory, workspaceId);
        var token = await AntiforgeryTestHelper.GetTokenAsync(manager, "/Documents/Create");
        var createResponse = await manager.PostAsync(
            "/Documents/Create",
            Form(
                token,
                ("Form.Title", "=SUM(1,1)"),
                ("Form.Description", "Synthetic spreadsheet safety test."),
                ("Form.CategoryId", categoryId.ToString()),
                ("Form.OwnerId", ownerId.ToString()),
                ("Form.EffectiveDate", "2026-03-15"),
                ("Form.ExpiryDate", string.Empty)));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        var managerResponse = await manager.GetAsync("/Documents/Export?Search=%3DSUM");
        using var managerWorkbook = new XLWorkbook(
            new MemoryStream(await managerResponse.Content.ReadAsByteArrayAsync()));
        var formulaCell = managerWorkbook.Worksheet("Documents").Cell(6, 2);
        Assert.Equal("=SUM(1,1)", formulaCell.GetString());
        Assert.True(formulaCell.Style.IncludeQuotePrefix);
        Assert.False(formulaCell.HasFormula);

        using var viewer = CreateClient(factory);
        await SignInAsync(viewer, "viewer@documents.demo");
        var viewerResponse = await viewer.GetAsync("/Documents/Export");
        using var viewerWorkbook = new XLWorkbook(
            new MemoryStream(await viewerResponse.Content.ReadAsByteArrayAsync()));
        var viewerTitles = ExportedTitles(viewerWorkbook.Worksheet("Documents"));

        Assert.Equal(10, viewerTitles.Count);
        Assert.DoesNotContain("Remote Work Guide", viewerTitles);
        Assert.Contains("Equipment Care Policy", viewerTitles);
    }

    [Fact]
    public async Task PdfSummaryContainsMetadataAndHistoryWithoutUploadedContent()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var manager = CreateClient(factory);
        var managerLogin = await SignInAsync(manager, "manager@documents.demo");
        var workspaceId = GetWorkspaceId(managerLogin);
        var activeId = await GetDocumentIdAsync(factory, workspaceId, "Equipment Care Policy");

        var response = await manager.GetAsync($"/Documents/SummaryPdf/{activeId}");
        var content = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("doc-0003-summary.pdf", DownloadFilename(response));
        Assert.True(content.Length is > 1_000 and < 250_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(content, 0, 4));
        Assert.DoesNotContain(
            "Synthetic portfolio demo file - no real records.",
            Encoding.Latin1.GetString(content),
            StringComparison.Ordinal);

        using var pdf = PdfReader.Open(new MemoryStream(content));
        Assert.Contains("DOC-0003", pdf.Info.Title, StringComparison.Ordinal);
        Assert.Equal("Document lifecycle metadata and history summary", pdf.Info.Subject);
        Assert.Contains("revisions:1", pdf.Info.Keywords, StringComparison.Ordinal);
        Assert.Contains("events:3", pdf.Info.Keywords, StringComparison.Ordinal);
        Assert.True(pdf.PageCount >= 1);

        using var viewer = CreateClient(factory);
        var viewerLogin = await SignInAsync(viewer, "viewer@documents.demo");
        var viewerWorkspaceId = GetWorkspaceId(viewerLogin);
        var viewerActiveId = await GetDocumentIdAsync(factory, viewerWorkspaceId, "Equipment Care Policy");
        var viewerDraftId = await GetDocumentIdAsync(factory, viewerWorkspaceId, "Remote Work Guide");
        Assert.Equal(
            HttpStatusCode.OK,
            (await viewer.GetAsync($"/Documents/SummaryPdf/{viewerActiveId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await viewer.GetAsync($"/Documents/SummaryPdf/{viewerDraftId}")).StatusCode);

        using var otherBrowser = CreateClient(factory);
        await SignInAsync(otherBrowser, "manager@documents.demo");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await otherBrowser.GetAsync($"/Documents/SummaryPdf/{activeId}")).StatusCode);
    }

    [Fact]
    public async Task AnonymousUsersCannotExportReports()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);

        var exportResponse = await client.GetAsync("/Documents/Export");
        var summaryResponse = await client.GetAsync($"/Documents/SummaryPdf/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Redirect, exportResponse.StatusCode);
        Assert.Contains("/Account/Login", exportResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, summaryResponse.StatusCode);
        Assert.Contains("/Account/Login", summaryResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    private static List<string> ExportedTitles(IXLWorksheet worksheet) => worksheet
        .RowsUsed()
        .Where(row => row.RowNumber() >= 6)
        .Select(row => row.Cell(2).GetString())
        .Where(value => value.Length > 0)
        .ToList();

    private static string? DownloadFilename(HttpResponseMessage response) =>
        response.Content.Headers.ContentDisposition?.FileNameStar ??
        response.Content.Headers.ContentDisposition?.FileName?.Trim('"');

    private static HttpClient CreateClient(DocumentLifecycleWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email)
    {
        var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/Account/Login");
        var response = await client.PostAsync(
            "/Account/Login",
            Form(token, ("Email", email), ("Password", DemoAccounts.SharedPassword)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response;
    }

    private static FormUrlEncodedContent Form(
        string token,
        params (string Key, string Value)[] fields)
    {
        var values = fields.ToDictionary(field => field.Key, field => field.Value);
        values["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(values);
    }

    private static Guid GetWorkspaceId(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("X-Demo-Workspace", out var values));
        return Guid.ParseExact(Assert.Single(values), "N");
    }

    private static async Task<(Guid CategoryId, Guid OwnerId)> GetReferenceIdsAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var categoryId = await database.DocumentCategories
            .Where(category => category.Name == "Procedure")
            .Select(category => category.PublicId)
            .SingleAsync();
        var ownerId = await database.DocumentOwners
            .Where(owner => owner.DisplayName == "Operations Team")
            .Select(owner => owner.PublicId)
            .SingleAsync();
        return (categoryId, ownerId);
    }

    private static async Task<Guid> GetDocumentIdAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ManagedDocuments
            .Where(document => document.Title == title)
            .Select(document => document.PublicId)
            .SingleAsync();
    }
}
