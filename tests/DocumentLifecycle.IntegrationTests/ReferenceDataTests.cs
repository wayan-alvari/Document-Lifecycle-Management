using System.Net;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class ReferenceDataTests
{
    [Fact]
    public async Task OnlyAdministratorCanOpenConfigurationPages()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var managerClient = CreateClient(factory);
        await SignInAsync(managerClient, "manager@documents.demo");

        var categoriesResponse = await managerClient.GetAsync("/Categories");
        var ownersResponse = await managerClient.GetAsync("/Owners");
        var dashboardHtml = await (await managerClient.GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Redirect, categoriesResponse.StatusCode);
        Assert.Contains("/Account/AccessDenied", categoriesResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, ownersResponse.StatusCode);
        Assert.Contains("/Account/AccessDenied", ownersResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.DoesNotContain("/Categories", dashboardHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("/Owners", dashboardHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministratorCanManageUnreferencedCategoryAndOwner()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "admin@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);

        var categoryToken = await AntiforgeryTestHelper.GetTokenAsync(client, "/Categories/Create");
        var categoryCreate = await client.PostAsync(
            "/Categories/Create",
            Form(categoryToken, ("Name", "Blueprint"), ("Description", "Fictional planning documents.")));
        Assert.Equal(HttpStatusCode.Redirect, categoryCreate.StatusCode);

        var ownerToken = await AntiforgeryTestHelper.GetTokenAsync(client, "/Owners/Create");
        var ownerCreate = await client.PostAsync(
            "/Owners/Create",
            Form(ownerToken, ("DisplayName", "Quality Team"), ("Contact", "quality@documents.demo")));
        Assert.Equal(HttpStatusCode.Redirect, ownerCreate.StatusCode);

        var (categoryId, ownerId) = await GetCreatedIdsAsync(factory, workspaceId);

        var categoryEditToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Categories/Edit/{categoryId}");
        var categoryEdit = await client.PostAsync(
            $"/Categories/Edit/{categoryId}",
            Form(categoryEditToken, ("Name", "Blueprints"), ("Description", "Updated fictional planning documents.")));
        Assert.Equal(HttpStatusCode.Redirect, categoryEdit.StatusCode);

        var ownerEditToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Owners/Edit/{ownerId}");
        var ownerEdit = await client.PostAsync(
            $"/Owners/Edit/{ownerId}",
            Form(ownerEditToken, ("DisplayName", "Quality Review Team"), ("Contact", "quality-review@documents.demo")));
        Assert.Equal(HttpStatusCode.Redirect, ownerEdit.StatusCode);

        var toggleToken = await AntiforgeryTestHelper.GetTokenAsync(client, "/Categories");
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await client.PostAsync($"/Categories/Toggle/{categoryId}", Form(toggleToken))).StatusCode);
        toggleToken = await AntiforgeryTestHelper.GetTokenAsync(client, "/Owners");
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await client.PostAsync($"/Owners/Toggle/{ownerId}", Form(toggleToken))).StatusCode);

        await VerifyUpdatedInactiveAsync(factory, workspaceId, categoryId, ownerId);

        var categoryDeleteToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Categories/Delete/{categoryId}");
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await client.PostAsync($"/Categories/Delete/{categoryId}", Form(categoryDeleteToken))).StatusCode);
        var ownerDeleteToken = await AntiforgeryTestHelper.GetTokenAsync(client, $"/Owners/Delete/{ownerId}");
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await client.PostAsync($"/Owners/Delete/{ownerId}", Form(ownerDeleteToken))).StatusCode);

        await VerifyDeletedWithAuditAsync(factory, workspaceId, categoryId, ownerId);
    }

    [Fact]
    public async Task DuplicateAndReferencedRecordsAreRejectedAndWorkspaceDataIsIsolated()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var firstClient = CreateClient(factory);
        var firstLogin = await SignInAsync(firstClient, "admin@documents.demo");
        var firstWorkspaceId = GetWorkspaceId(firstLogin);

        var createToken = await AntiforgeryTestHelper.GetTokenAsync(firstClient, "/Categories/Create");
        var createResponse = await firstClient.PostAsync(
            "/Categories/Create",
            Form(createToken, ("Name", "Private Label"), ("Description", "Visible in one browser only.")));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        var duplicateToken = await AntiforgeryTestHelper.GetTokenAsync(firstClient, "/Categories/Create");
        var duplicateResponse = await firstClient.PostAsync(
            "/Categories/Create",
            Form(duplicateToken, ("Name", "policy"), ("Description", "Duplicate name check.")));
        var duplicateHtml = await duplicateResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Contains("A category with this name already exists.", duplicateHtml, StringComparison.Ordinal);

        var policyId = await GetCategoryIdAsync(factory, firstWorkspaceId, "Policy");
        var deleteToken = await AntiforgeryTestHelper.GetTokenAsync(firstClient, "/Categories");
        var deleteResponse = await firstClient.PostAsync($"/Categories/Delete/{policyId}", Form(deleteToken));
        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);
        var categoryHtml = await (await firstClient.GetAsync("/Categories")).Content.ReadAsStringAsync();
        Assert.Contains("referenced by documents", categoryHtml, StringComparison.Ordinal);
        Assert.Contains("Private Label", categoryHtml, StringComparison.Ordinal);

        using var secondClient = CreateClient(factory);
        await SignInAsync(secondClient, "admin@documents.demo");
        var secondHtml = await (await secondClient.GetAsync("/Categories")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Private Label", secondHtml, StringComparison.Ordinal);
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

    private static async Task<(Guid CategoryId, Guid OwnerId)> GetCreatedIdsAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var categoryId = await database.DocumentCategories
            .Where(category => category.Name == "Blueprint")
            .Select(category => category.PublicId)
            .SingleAsync();
        var ownerId = await database.DocumentOwners
            .Where(owner => owner.DisplayName == "Quality Team")
            .Select(owner => owner.PublicId)
            .SingleAsync();
        return (categoryId, ownerId);
    }

    private static async Task VerifyUpdatedInactiveAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        Guid categoryId,
        Guid ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = await database.DocumentCategories.SingleAsync(item => item.PublicId == categoryId);
        var owner = await database.DocumentOwners.SingleAsync(item => item.PublicId == ownerId);
        Assert.Equal("Blueprints", category.Name);
        Assert.False(category.IsActive);
        Assert.Equal("Quality Review Team", owner.DisplayName);
        Assert.False(owner.IsActive);
    }

    private static async Task VerifyDeletedWithAuditAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        Guid categoryId,
        Guid ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await database.DocumentCategories.AnyAsync(item => item.PublicId == categoryId));
        Assert.False(await database.DocumentOwners.AnyAsync(item => item.PublicId == ownerId));
        Assert.True(await database.AuditEvents.AnyAsync(item => item.Action == "CategoryDeleted"));
        Assert.True(await database.AuditEvents.AnyAsync(item => item.Action == "OwnerDeleted"));
    }

    private static async Task<Guid> GetCategoryIdAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .DocumentCategories
            .Where(category => category.Name == name)
            .Select(category => category.PublicId)
            .SingleAsync();
    }
}
