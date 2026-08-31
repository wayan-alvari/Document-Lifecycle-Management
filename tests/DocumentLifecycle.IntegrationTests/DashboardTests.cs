using System.Net;
using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocumentLifecycle.IntegrationTests;

public sealed class DashboardTests
{
    [Fact]
    public async Task AuthenticatedDashboardShowsSeededMetricsExpiryAndActivity()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/Account/Login");
        var loginResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "viewer@documents.demo",
                ["Password"] = DemoAccounts.SharedPassword,
                ["__RequestVerificationToken"] = token,
            }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Lifecycle dashboard", html, StringComparison.Ordinal);
        Assert.Contains("Expiring soon", html, StringComparison.Ordinal);
        Assert.Contains(">12</strong>", html, StringComparison.Ordinal);
        Assert.Contains(">3</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Emergency Contact Procedure", html, StringComparison.Ordinal);
        Assert.Contains("Revision uploaded", html, StringComparison.Ordinal);
        Assert.Contains("<progress", html, StringComparison.Ordinal);
        Assert.Contains("data-lte-toggle=\"sidebar\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("â", html, StringComparison.Ordinal);
    }
}
