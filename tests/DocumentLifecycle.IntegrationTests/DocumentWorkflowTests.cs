using System.Net;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class DocumentWorkflowTests
{
    [Fact]
    public async Task ManagerCanCreateEditAndActivateEligibleDraft()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "manager@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var (categoryId, ownerId, eligibleDraftId) = await GetSetupIdsAsync(factory, workspaceId);

        var createToken = await AntiforgeryTestHelper.GetTokenAsync(client, "/Documents/Create");
        var createResponse = await client.PostAsync(
            "/Documents/Create",
            Form(
                createToken,
                ("Form.Title", "Fictional Launch Checklist"),
                ("Form.Description", "A synthetic draft created by the integration test."),
                ("Form.CategoryId", categoryId.ToString()),
                ("Form.OwnerId", ownerId.ToString()),
                ("Form.EffectiveDate", "2026-01-15"),
                ("Form.ExpiryDate", "2026-05-15")));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        var createdId = await GetDocumentIdAsync(factory, workspaceId, "Fictional Launch Checklist");
        var detailsHtml = await client.GetStringAsync($"/Documents/Details/{createdId}");
        Assert.Contains("Fictional Launch Checklist", detailsHtml, StringComparison.Ordinal);
        Assert.Contains("DOC-2026-", detailsHtml, StringComparison.Ordinal);
        Assert.Contains("Upload at least one valid revision", detailsHtml, StringComparison.Ordinal);

        var editToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/Edit/{createdId}");
        var editResponse = await client.PostAsync(
            $"/Documents/Edit/{createdId}",
            Form(
                editToken,
                ("Form.Title", "Fictional Launch Readiness Checklist"),
                ("Form.Description", "Updated synthetic draft metadata."),
                ("Form.CategoryId", categoryId.ToString()),
                ("Form.OwnerId", ownerId.ToString()),
                ("Form.EffectiveDate", "2026-01-20"),
                ("Form.ExpiryDate", "2026-06-01")));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        var ineligibleToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/Details/{createdId}");
        var ineligibleActivation = await client.PostAsync(
            $"/Documents/Activate/{createdId}",
            Form(ineligibleToken));
        Assert.Equal(HttpStatusCode.Redirect, ineligibleActivation.StatusCode);
        var rejectedHtml = await client.GetStringAsync($"/Documents/Details/{createdId}");
        Assert.Contains("at least one revision", rejectedHtml, StringComparison.OrdinalIgnoreCase);

        var activateToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Documents/Details/{eligibleDraftId}");
        var activateResponse = await client.PostAsync(
            $"/Documents/Activate/{eligibleDraftId}",
            Form(activateToken));
        Assert.Equal(HttpStatusCode.Redirect, activateResponse.StatusCode);

        await VerifyWorkflowAsync(factory, workspaceId, createdId, eligibleDraftId);
    }

    [Fact]
    public async Task ViewerCannotSeeDraftsOrUseManagerCommands()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "viewer@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        var draftId = await GetDocumentIdAsync(factory, workspaceId, "Remote Work Guide");

        var listResponse = await client.GetAsync("/Documents");
        var listHtml = await listResponse.Content.ReadAsStringAsync();
        var detailsResponse = await client.GetAsync($"/Documents/Details/{draftId}");
        var createResponse = await client.GetAsync("/Documents/Create");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.DoesNotContain("Remote Work Guide", listHtml, StringComparison.Ordinal);
        Assert.Contains("Equipment Care Policy", listHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Contains("/Account/AccessDenied", createResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerSideFiltersAndPublicIdsRemainWorkspaceScoped()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var firstClient = CreateClient(factory);
        var firstLogin = await SignInAsync(firstClient, "manager@documents.demo");
        var firstWorkspaceId = GetWorkspaceId(firstLogin);
        var (categoryId, ownerId, _) = await GetSetupIdsAsync(factory, firstWorkspaceId);
        var createToken = await AntiforgeryTestHelper.GetTokenAsync(firstClient, "/Documents/Create");
        await firstClient.PostAsync(
            "/Documents/Create",
            Form(
                createToken,
                ("Form.Title", "Browser Private Draft"),
                ("Form.Description", "Synthetic isolation record."),
                ("Form.CategoryId", categoryId.ToString()),
                ("Form.OwnerId", ownerId.ToString()),
                ("Form.EffectiveDate", "2026-01-15"),
                ("Form.ExpiryDate", string.Empty)));
        var privateId = await GetDocumentIdAsync(factory, firstWorkspaceId, "Browser Private Draft");

        var filteredHtml = await firstClient.GetStringAsync(
            "/Documents?Status=ExpiringSoon&Search=Records");
        Assert.Contains("Records Handling Policy", filteredHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Training Provider Certificate", filteredHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Remote Work Guide", filteredHtml, StringComparison.Ordinal);

        using var secondClient = CreateClient(factory);
        await SignInAsync(secondClient, "manager@documents.demo");
        var secondListHtml = await secondClient.GetStringAsync("/Documents?Search=Browser+Private");
        var crossWorkspaceDetails = await secondClient.GetAsync($"/Documents/Details/{privateId}");
        var crossWorkspaceEdit = await secondClient.GetAsync($"/Documents/Edit/{privateId}");

        Assert.DoesNotContain("Browser Private Draft", secondListHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, crossWorkspaceDetails.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossWorkspaceEdit.StatusCode);
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

    private static async Task<(Guid CategoryId, Guid OwnerId, Guid EligibleDraftId)> GetSetupIdsAsync(
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
        var eligibleDraftId = await database.ManagedDocuments
            .Where(document => document.Title == "Visitor Check-in Draft")
            .Select(document => document.PublicId)
            .SingleAsync();
        return (categoryId, ownerId, eligibleDraftId);
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

    private static async Task VerifyWorkflowAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        Guid createdId,
        Guid eligibleDraftId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var created = await database.ManagedDocuments.SingleAsync(document => document.PublicId == createdId);
        var activated = await database.ManagedDocuments.SingleAsync(document => document.PublicId == eligibleDraftId);
        Assert.Equal("Fictional Launch Readiness Checklist", created.Title);
        Assert.Equal(LifecycleState.Draft, created.State);
        Assert.Equal(LifecycleState.Active, activated.State);
        Assert.True(await database.AuditEvents.AnyAsync(audit =>
            audit.EntityPublicId == eligibleDraftId && audit.Action == "Activated"));
    }
}
