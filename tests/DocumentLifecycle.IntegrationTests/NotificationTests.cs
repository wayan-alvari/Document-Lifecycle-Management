using System.Net;
using DocumentLifecycle.Application.Notifications;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class NotificationTests
{
    [Fact]
    public async Task ExpiryNotificationsAreRoleTargetedAndIdempotentAcrossBoundaryChanges()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "viewer@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);

        var firstResponse = await client.GetAsync("/Notifications");
        var firstHtml = await firstResponse.Content.ReadAsStringAsync();
        var secondResponse = await client.GetAsync("/Notifications");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains("5 unread", firstHtml, StringComparison.Ordinal);
        Assert.Contains("is due for review", firstHtml, StringComparison.Ordinal);
        Assert.Contains("passed its review date", firstHtml, StringComparison.Ordinal);
        await AssertNotificationCountsAsync(factory, workspaceId, total: 15, viewerUnread: 5);

        factory.Clock.Advance(TimeSpan.FromDays(1));
        await RefreshNotificationsAsync(factory, workspaceId);
        await RefreshNotificationsAsync(factory, workspaceId);

        await AssertNotificationCountsAsync(factory, workspaceId, total: 18, viewerUnread: 6);
    }

    [Fact]
    public async Task ViewerCanMarkOneAndAllWithoutChangingOtherRoleNotifications()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginResponse = await SignInAsync(client, "viewer@documents.demo");
        var workspaceId = GetWorkspaceId(loginResponse);
        _ = await client.GetAsync("/Notifications");
        var notificationId = await GetFirstViewerNotificationIdAsync(factory, workspaceId);

        var markOneToken = await AntiforgeryTestHelper.GetTokenAsync(client, "/Notifications");
        var markOneResponse = await client.PostAsync(
            $"/Notifications/MarkRead/{notificationId}",
            Form(markOneToken));
        Assert.Equal(HttpStatusCode.Redirect, markOneResponse.StatusCode);
        await AssertNotificationCountsAsync(factory, workspaceId, total: 15, viewerUnread: 4);

        var markAllToken = await AntiforgeryTestHelper.GetTokenAsync(client, "/Notifications");
        var markAllResponse = await client.PostAsync(
            "/Notifications/MarkAllRead",
            Form(markAllToken));
        Assert.Equal(HttpStatusCode.Redirect, markAllResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(
            0,
            await database.Notifications.CountAsync(notification =>
                notification.RecipientRole == ApplicationRoles.Viewer && notification.ReadAtUtc == null));
        Assert.Equal(
            5,
            await database.Notifications.CountAsync(notification =>
                notification.RecipientRole == ApplicationRoles.DocumentManager && notification.ReadAtUtc == null));
    }

    [Fact]
    public async Task NotificationPublicIdsAndRowsAreWorkspaceIsolated()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var firstClient = CreateClient(factory);
        var firstLogin = await SignInAsync(firstClient, "viewer@documents.demo");
        var firstWorkspaceId = GetWorkspaceId(firstLogin);
        _ = await firstClient.GetAsync("/Notifications");
        var firstNotificationId = await GetFirstViewerNotificationIdAsync(factory, firstWorkspaceId);

        using var secondClient = CreateClient(factory);
        var secondLogin = await SignInAsync(secondClient, "viewer@documents.demo");
        var secondWorkspaceId = GetWorkspaceId(secondLogin);
        _ = await secondClient.GetAsync("/Notifications");
        var token = await AntiforgeryTestHelper.GetTokenAsync(secondClient, "/Notifications");
        var crossWorkspaceMark = await secondClient.PostAsync(
            $"/Notifications/MarkRead/{firstNotificationId}",
            Form(token));

        Assert.NotEqual(firstWorkspaceId, secondWorkspaceId);
        Assert.Equal(HttpStatusCode.NotFound, crossWorkspaceMark.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(30, await database.Notifications.IgnoreQueryFilters().CountAsync());
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

    private static async Task<Guid> GetFirstViewerNotificationIdAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Notifications
            .Where(notification => notification.RecipientRole == ApplicationRoles.Viewer)
            .OrderBy(notification => notification.Id)
            .Select(notification => notification.PublicId)
            .FirstAsync();
    }

    private static async Task AssertNotificationCountsAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId,
        int total,
        int viewerUnread)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(total, await database.Notifications.CountAsync());
        Assert.Equal(
            viewerUnread,
            await database.Notifications.CountAsync(notification =>
                notification.RecipientRole == ApplicationRoles.Viewer && notification.ReadAtUtc == null));
    }

    private static async Task RefreshNotificationsAsync(
        DocumentLifecycleWebApplicationFactory factory,
        Guid workspaceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentWorkspace>().Set(workspaceId);
        await scope.ServiceProvider.GetRequiredService<INotificationService>()
            .RefreshExpiryNotificationsAsync();
    }
}
