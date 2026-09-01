using System.Net;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class AuditWorkflowTests
{
    [Fact]
    public async Task ManagerCanArchiveAndRestoreWithDocumentContextAudit()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "manager@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var documentId = await GetDocumentIdAsync(factory, workspaceId, "Equipment Care Policy");

        var archiveToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/Archive/{documentId}");
        var archiveResponse = await client.PostAsync(
            $"/Documents/Archive/{documentId}",
            Form(archiveToken, ("Form.Reason", "Replaced by a fictional updated care standard.")));
        Assert.Equal(HttpStatusCode.Redirect, archiveResponse.StatusCode);

        var archivedHtml = await client.GetStringAsync($"/Documents/Details/{documentId}");
        Assert.Contains("Archived", archivedHtml, StringComparison.Ordinal);
        Assert.Contains("Replaced by a fictional updated care standard.", archivedHtml, StringComparison.Ordinal);
        Assert.Contains("Document activity", archivedHtml, StringComparison.Ordinal);
        await VerifyArchivedAsync(factory, workspaceId, documentId);

        var restoreToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/Details/{documentId}");
        var restoreResponse = await client.PostAsync(
            $"/Documents/Restore/{documentId}",
            Form(restoreToken));
        Assert.Equal(HttpStatusCode.Redirect, restoreResponse.StatusCode);

        var restoredHtml = await client.GetStringAsync($"/Documents/Details/{documentId}");
        Assert.Contains("Restored", restoredHtml, StringComparison.Ordinal);
        await VerifyRestoredAsync(factory, workspaceId, documentId);

        var managerAudit = await client.GetAsync("/Audit");
        Assert.Equal(HttpStatusCode.Redirect, managerAudit.StatusCode);
        Assert.Contains("/Account/AccessDenied", managerAudit.Headers.Location?.OriginalString, StringComparison.Ordinal);

        await SwitchRoleAsync(client, "admin@documents.demo");
        var auditResponse = await client.GetAsync(
            "/Audit?Search=Equipment&EventAction=Archived");
        var auditHtml = await auditResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        Assert.Contains("Equipment Care Policy", auditHtml, StringComparison.Ordinal);
        Assert.Contains("reason: Replaced by a fictional updated care standard.", auditHtml, StringComparison.Ordinal);

        using var otherBrowser = CreateClient(factory);
        await SignInAsync(otherBrowser, "admin@documents.demo");
        var isolatedAuditHtml = await otherBrowser.GetStringAsync(
            "/Audit?Search=fictional+updated+care&EventAction=Archived");
        Assert.DoesNotContain("Replaced by a fictional updated care standard.", isolatedAuditHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewerCanSearchFullAuditButCannotArchiveOrRestore()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "viewer@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var activeId = await GetDocumentIdAsync(factory, workspaceId, "Equipment Care Policy");

        var auditResponse = await client.GetAsync("/Audit?Search=Equipment&EventAction=Activated");
        var auditHtml = await auditResponse.Content.ReadAsStringAsync();
        var archiveResponse = await client.GetAsync($"/Documents/Archive/{activeId}");

        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        Assert.Contains("Equipment Care Policy", auditHtml, StringComparison.Ordinal);
        Assert.Contains("Activated", auditHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, archiveResponse.StatusCode);
        Assert.Contains("/Account/AccessDenied", archiveResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidArchiveAndRestoreTransitionsAreRejectedServerSide()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "manager@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var draftId = await GetDocumentIdAsync(factory, workspaceId, "Remote Work Guide");
        var activeId = await GetDocumentIdAsync(factory, workspaceId, "Equipment Care Policy");

        var draftArchive = await client.GetAsync($"/Documents/Archive/{draftId}");
        Assert.Equal(HttpStatusCode.Redirect, draftArchive.StatusCode);
        var draftHtml = await client.GetStringAsync($"/Documents/Details/{draftId}");
        Assert.Contains("Only active documents can be archived.", draftHtml, StringComparison.Ordinal);

        var restoreToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/Details/{activeId}");
        var activeRestore = await client.PostAsync(
            $"/Documents/Restore/{activeId}",
            Form(restoreToken));
        Assert.Equal(HttpStatusCode.Redirect, activeRestore.StatusCode);
        var activeHtml = await client.GetStringAsync($"/Documents/Details/{activeId}");
        Assert.Contains("Only archived documents can be restored.", activeHtml, StringComparison.Ordinal);
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

    private static async Task SwitchRoleAsync(HttpClient client, string email)
    {
        var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/");
        var logout = await client.PostAsync("/Account/Logout", Form(token));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        await SignInAsync(client, email);
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

    private static async Task VerifyArchivedAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        Guid documentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await database.ManagedDocuments.SingleAsync(item => item.PublicId == documentId);
        Assert.Equal(LifecycleState.Archived, document.State);
        Assert.Equal("Replaced by a fictional updated care standard.", document.ArchiveReason);
        Assert.True(await database.AuditEvents.AnyAsync(audit =>
            audit.EntityPublicId == documentId && audit.Action == "Archived"));
    }

    private static async Task VerifyRestoredAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        Guid documentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var document = await database.ManagedDocuments.SingleAsync(item => item.PublicId == documentId);
        Assert.Equal(LifecycleState.Active, document.State);
        Assert.Null(document.ArchiveReason);
        Assert.Null(document.ArchivedBy);
        Assert.Null(document.ArchivedAtUtc);
        Assert.True(await database.AuditEvents.AnyAsync(audit =>
            audit.EntityPublicId == documentId && audit.Action == "Restored"));
    }
}
