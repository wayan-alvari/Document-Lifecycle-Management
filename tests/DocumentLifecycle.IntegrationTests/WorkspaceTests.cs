using System.Net;
using System.Net.Http.Headers;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Domain.Workspaces;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.Workspaces;
using DocumentLifecycle.Web.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class WorkspaceTests
{
    [Fact]
    public async Task SeparateBrowsersReceiveProtectedIsolatedWorkspaceIds()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var firstResponse = await firstClient.GetAsync("/Account/Login");
        var secondResponse = await secondClient.GetAsync("/Account/Login");
        var firstId = GetWorkspaceId(firstResponse);
        var secondId = GetWorkspaceId(secondResponse);

        Assert.NotEqual(firstId, secondId);
        var firstCookie = Assert.Single(
            firstResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(DemoWorkspaceCookie.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(firstId.ToString("N"), firstCookie, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await database.DemoWorkspaces.CountAsync());
    }

    [Fact]
    public async Task WorkspacePersistsAcrossAuthenticationAndRoleSwitchLogout()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateBrowserClient(factory);

        var loginPage = await client.GetAsync("/Account/Login");
        var workspaceId = GetWorkspaceId(loginPage);
        var loginResponse = await SignInAsync(client, "admin@documents.demo");
        Assert.Equal(workspaceId, GetWorkspaceId(loginResponse));

        var homeResponse = await client.GetAsync("/");
        Assert.Equal(workspaceId, GetWorkspaceId(homeResponse));
        var logoutToken = await AntiforgeryTestHelper.GetTokenAsync(client, "/");
        var logoutResponse = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = logoutToken,
            }));

        Assert.Equal(workspaceId, GetWorkspaceId(logoutResponse));
        var switchedLogin = await client.GetAsync("/Account/Login");
        Assert.Equal(workspaceId, GetWorkspaceId(switchedLogin));

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await database.DemoWorkspaces.CountAsync());
    }

    [Fact]
    public async Task ExpiredWorkspaceResetsAtSixHoursAndDeletesItsUploads()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = factory.CreateClient();

        var initialResponse = await client.GetAsync("/Account/Login");
        var initialId = GetWorkspaceId(initialResponse);
        var uploadDirectory = Path.Combine(factory.UploadRoot, initialId.ToString("N"));
        Directory.CreateDirectory(uploadDirectory);
        await File.WriteAllTextAsync(Path.Combine(uploadDirectory, "synthetic.txt"), "fictional test content");

        factory.Clock.Advance(TimeSpan.FromHours(5) + TimeSpan.FromMinutes(59));
        var beforeExpiry = await client.GetAsync("/Account/Login");
        Assert.Equal(initialId, GetWorkspaceId(beforeExpiry));

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var resetResponse = await client.GetAsync("/Account/Login");
        var resetId = GetWorkspaceId(resetResponse);

        Assert.NotEqual(initialId, resetId);
        Assert.False(Directory.Exists(uploadDirectory));

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await database.DemoWorkspaces.AnyAsync(workspace => workspace.Id == initialId));
        Assert.True(await database.DemoWorkspaces.AnyAsync(workspace => workspace.Id == resetId));
        Assert.Equal(1, await database.DemoWorkspaces.CountAsync());
        Assert.False(await database.ManagedDocuments
            .IgnoreQueryFilters()
            .AnyAsync(document => document.WorkspaceId == initialId));
        Assert.False(await database.DocumentRevisions
            .IgnoreQueryFilters()
            .AnyAsync(revision => revision.WorkspaceId == initialId));
        Assert.False(await database.AuditEvents
            .IgnoreQueryFilters()
            .AnyAsync(auditEvent => auditEvent.WorkspaceId == initialId));
        Assert.Equal(12, await database.ManagedDocuments.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task AuthenticatedNavigationUpdatesActivityOnlyAfterThrottleInterval()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateBrowserClient(factory);

        var loginResponse = await SignInAsync(client, "viewer@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        _ = await client.GetAsync("/");

        var initialActivity = await GetLastActivityAsync(factory, workspaceId);
        factory.Clock.Advance(TimeSpan.FromMinutes(4));
        _ = await client.GetAsync("/");
        Assert.Equal(initialActivity, await GetLastActivityAsync(factory, workspaceId));

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await client.GetAsync("/");
        Assert.Equal(factory.Clock.UtcNow, await GetLastActivityAsync(factory, workspaceId));

        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        using var pollingRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        pollingRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        pollingRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
        _ = await client.SendAsync(pollingRequest);
        Assert.Equal(factory.Clock.UtcNow.AddMinutes(-5), await GetLastActivityAsync(factory, workspaceId));
    }

    [Fact]
    public async Task HealthAndStaticAssetRequestsDoNotCreateAWorkspace()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = factory.CreateClient();

        var healthResponse = await client.GetAsync("/health");
        var staticResponse = await client.GetAsync("/vendor/bootstrap/bootstrap.min.css");

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, staticResponse.StatusCode);
        Assert.False(healthResponse.Headers.Contains("X-Demo-Workspace"));
        Assert.False(staticResponse.Headers.Contains("X-Demo-Workspace"));

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await database.DemoWorkspaces.CountAsync());
    }

    [Fact]
    public async Task CleanupRunnerRemovesExpiredRowsAndFiles()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        _ = factory.Services;
        var expiredId = Guid.NewGuid();
        var expired = DemoWorkspace.Create(
            expiredId,
            factory.Clock.UtcNow.Subtract(TimeSpan.FromHours(7)),
            seedVersion: 1);
        var uploadDirectory = Path.Combine(factory.UploadRoot, expiredId.ToString("N"));
        Directory.CreateDirectory(uploadDirectory);
        await File.WriteAllTextAsync(Path.Combine(uploadDirectory, "synthetic.txt"), "fictional test content");

        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var database = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            database.DemoWorkspaces.Add(expired);
            await database.SaveChangesAsync();
        }

        await using (var cleanupScope = factory.Services.CreateAsyncScope())
        {
            var runner = cleanupScope.ServiceProvider.GetRequiredService<WorkspaceCleanupRunner>();
            Assert.Equal(1, await runner.CleanupExpiredAsync());
        }

        Assert.False(Directory.Exists(uploadDirectory));
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verificationDatabase.DemoWorkspaces.AnyAsync(workspace => workspace.Id == expiredId));
    }

    [Fact]
    public async Task TamperedCookieIsRejectedAndReplaced()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Login");
        request.Headers.Add("Cookie", $"{DemoWorkspaceCookie.Name}=tampered");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, GetWorkspaceId(response));
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(DemoWorkspaceCookie.Name, StringComparison.Ordinal));
    }

    private static HttpClient CreateBrowserClient(DocumentLifecycleWebApplicationFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        return client;
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email)
    {
        var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/Account/Login");
        return await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = DemoAccounts.SharedPassword,
                ["__RequestVerificationToken"] = token,
            }));
    }

    private static Guid GetWorkspaceId(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("X-Demo-Workspace", out var values));
        return Guid.ParseExact(Assert.Single(values), "N");
    }

    private static async Task<DateTime> GetLastActivityAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await database.DemoWorkspaces
            .Where(workspace => workspace.Id == workspaceId)
            .Select(workspace => workspace.LastActivityAtUtc)
            .SingleAsync();
    }
}
