using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class FileSecurityTests
{
    public static IEnumerable<object[]> AllowedImageUploads()
    {
        yield return
        [
            "synthetic.png",
            "image/png",
            new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00 },
        ];
        yield return
        [
            "synthetic.jpeg",
            "image/jpeg",
            new byte[] { 0xff, 0xd8, 0xff, 0xe0, 0x00, 0x00, 0xff, 0xd9 },
        ];
    }

    [Fact]
    public async Task ManagerUploadIsSanitizedHashedPrivateAuditedAndDownloadable()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "manager@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var documentId = await GetDocumentIdAsync(factory, workspaceId, "Remote Work Guide");
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\nSynthetic upload test\n%%EOF\n");

        var token = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/UploadRevision/{documentId}");
        using var upload = Multipart(
            token,
            "Initial fictional revision",
            "../../private/confidential.pdf",
            "application/pdf",
            pdf);
        var uploadResponse = await client.PostAsync($"/Documents/UploadRevision/{documentId}", upload);
        Assert.Equal(HttpStatusCode.Redirect, uploadResponse.StatusCode);

        var evidence = await GetRevisionEvidenceAsync(factory, workspaceId, documentId);
        Assert.Equal("confidential.pdf", evidence.OriginalFilename);
        Assert.Matches("^[0-9a-f]{32}\\.pdf$", evidence.StoredFilename);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(), evidence.Sha256Hash);
        Assert.True(File.Exists(evidence.PhysicalPath));
        Assert.Equal(pdf, await File.ReadAllBytesAsync(evidence.PhysicalPath));

        var activateToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/Details/{documentId}");
        var activateResponse = await client.PostAsync(
            $"/Documents/Activate/{documentId}",
            Form(activateToken));
        Assert.Equal(HttpStatusCode.Redirect, activateResponse.StatusCode);

        var downloadResponse = await client.GetAsync(
            $"/Documents/Download/{documentId}?revisionId={evidence.RevisionId}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdf, await downloadResponse.Content.ReadAsByteArrayAsync());
        Assert.Contains("confidential.pdf", downloadResponse.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("..", downloadResponse.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
        Assert.Contains("nosniff", downloadResponse.Headers.GetValues("X-Content-Type-Options"));
        Assert.True(downloadResponse.Headers.CacheControl?.NoStore);
        Assert.True(downloadResponse.Headers.CacheControl?.Private);
        Assert.NotNull(downloadResponse.Headers.ETag);

        using var otherBrowser = CreateClient(factory);
        await SignInAsync(otherBrowser, "manager@documents.demo");
        var crossWorkspaceDownload = await otherBrowser.GetAsync(
            $"/Documents/Download/{documentId}?revisionId={evidence.RevisionId}");
        Assert.Equal(HttpStatusCode.NotFound, crossWorkspaceDownload.StatusCode);
    }

    [Fact]
    public async Task ViewerCanDownloadActiveButNotDraftAndCannotUpload()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "viewer@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var active = await GetDocumentRevisionIdsAsync(factory, workspaceId, "Equipment Care Policy");
        var draft = await GetDocumentRevisionIdsAsync(factory, workspaceId, "Visitor Check-in Draft");

        var activeDownload = await client.GetAsync(
            $"/Documents/Download/{active.DocumentId}?revisionId={active.RevisionId}");
        var draftDownload = await client.GetAsync(
            $"/Documents/Download/{draft.DocumentId}?revisionId={draft.RevisionId}");
        var uploadPage = await client.GetAsync($"/Documents/UploadRevision/{active.DocumentId}");

        Assert.Equal(HttpStatusCode.OK, activeDownload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draftDownload.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, uploadPage.StatusCode);
        Assert.Contains("/Account/AccessDenied", uploadPage.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllowedImageUploads))]
    public async Task AllowedImageSignaturesCanBeUploaded(
        string filename,
        string mediaType,
        byte[] content)
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "manager@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var documentId = await GetDocumentIdAsync(factory, workspaceId, "Remote Work Guide");

        var token = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/UploadRevision/{documentId}");
        using var upload = Multipart(token, "Synthetic image revision", filename, mediaType, content);
        var response = await client.PostAsync($"/Documents/UploadRevision/{documentId}", upload);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task InvalidTypeSignatureAndOversizeUploadsLeaveNoRevisionOrTemporaryFile()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "manager@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var documentId = await GetDocumentIdAsync(factory, workspaceId, "Remote Work Guide");

        var badSignature = await PostUploadAsync(
            client,
            documentId,
            "bad.pdf",
            "application/pdf",
            Encoding.ASCII.GetBytes("This is not a PDF."));
        Assert.Contains("file signature", await badSignature.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var mismatchedType = await PostUploadAsync(
            client,
            documentId,
            "image.png",
            "application/pdf",
            Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF"));
        Assert.Contains("extension and media type", await mismatchedType.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var forbiddenType = await PostUploadAsync(
            client,
            documentId,
            "drawing.svg",
            "image/svg+xml",
            Encoding.UTF8.GetBytes("<svg></svg>"));
        Assert.Contains("PDF, PNG, JPG, or JPEG", await forbiddenType.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var oversized = await PostUploadAsync(
            client,
            documentId,
            "large.pdf",
            "application/pdf",
            new byte[(10 * 1024 * 1024) + 1]);
        Assert.Contains("cannot exceed 10 MB", await oversized.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(
            0,
            await database.DocumentRevisions.CountAsync(revision =>
                revision.ManagedDocumentId == database.ManagedDocuments
                    .Where(document => document.PublicId == documentId)
                    .Select(document => document.Id)
                    .Single()));
        var workspacePath = Path.Combine(factory.UploadRoot, workspaceId.ToString("N"));
        Assert.DoesNotContain(Directory.GetFiles(workspacePath), path => path.EndsWith(".uploading", StringComparison.Ordinal));
    }

    private static async Task<HttpResponseMessage> PostUploadAsync(
        HttpClient client,
        Guid documentId,
        string filename,
        string mediaType,
        byte[] content)
    {
        var token = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/UploadRevision/{documentId}");
        using var multipart = Multipart(token, "Synthetic rejected upload", filename, mediaType, content);
        var response = await client.PostAsync($"/Documents/UploadRevision/{documentId}", multipart);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    private static MultipartFormDataContent Multipart(
        string token,
        string changeNote,
        string filename,
        string mediaType,
        byte[] content)
    {
        var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(token), "__RequestVerificationToken");
        multipart.Add(new StringContent(changeNote), "Form.ChangeNote");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        multipart.Add(file, "Form.Upload", filename);
        return multipart;
    }

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

    private static async Task<RevisionEvidence> GetRevisionEvidenceAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        Guid documentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var revision = await database.DocumentRevisions
            .Where(item => database.ManagedDocuments
                .Where(document => document.PublicId == documentId)
                .Select(document => document.Id)
                .Contains(item.ManagedDocumentId))
            .SingleAsync();
        Assert.True(await database.AuditEvents.AnyAsync(audit =>
            audit.EntityPublicId == documentId && audit.Action == "RevisionUploaded"));
        return new RevisionEvidence(
            revision.PublicId,
            revision.OriginalFilename,
            revision.StoredFilename,
            revision.Sha256Hash,
            Path.Combine(factory.UploadRoot, workspaceId.ToString("N"), revision.StoredFilename));
    }

    private static async Task<(Guid DocumentId, Guid RevisionId)> GetDocumentRevisionIdsAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await database.ManagedDocuments
            .Include(item => item.Revisions)
            .SingleAsync(item => item.Title == title);
        return (document.PublicId, Assert.Single(document.Revisions).PublicId);
    }

    private sealed record RevisionEvidence(
        Guid RevisionId,
        string OriginalFilename,
        string StoredFilename,
        string Sha256Hash,
        string PhysicalPath);
}
