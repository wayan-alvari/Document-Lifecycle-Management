using System.Net;
using DocumentLifecycle.Application.Dashboard;
using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocumentLifecycle.IntegrationTests;

public sealed class HardeningTests
{
    [Fact]
    public async Task SecurityHeadersProtectDynamicStaticAndHealthResponses()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);

        var login = await client.GetAsync("/Account/Login");
        var staticAsset = await client.GetAsync("/css/site.css");
        var health = await client.GetAsync("/health");

        AssertSecurityHeaders(login);
        AssertSecurityHeaders(staticAsset);
        AssertSecurityHeaders(health);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("Healthy", await health.Content.ReadAsStringAsync());
        Assert.Contains("no-store", health.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.False(health.Headers.Contains("X-Demo-Workspace"));
    }

    [Fact]
    public async Task UnknownRouteUsesFriendlyNotFoundPage()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/this-page-does-not-exist");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page not found", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Development Mode", html, StringComparison.Ordinal);
        Assert.DoesNotContain("D:\\", html, StringComparison.Ordinal);
        AssertSecurityHeaders(response);

        var invalidStatus = await client.GetAsync("/Home/StatusPage?code=999999");
        Assert.Equal(HttpStatusCode.NotFound, invalidStatus.StatusCode);
    }

    [Fact]
    public async Task UnhandledExceptionUsesSafeCentralizedErrorPage()
    {
        await using var baseFactory = new DocumentLifecycleWebApplicationFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDashboardQuery>();
                services.AddScoped<IDashboardQuery>(_ => new ThrowingDashboardQuery());
            }));
        using var client = CreateClient(factory);
        await SignInAsync(client, "manager@documents.demo", DemoAccounts.SharedPassword);

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("We could not complete that request", html, StringComparison.Ordinal);
        Assert.Contains("Reference:", html, StringComparison.Ordinal);
        Assert.DoesNotContain(ThrowingDashboardQuery.SensitiveMessage, html, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ThrowingDashboardQuery), html, StringComparison.Ordinal);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task LoginRateLimitRejectsEleventhAttemptPerClientAddress()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        var responses = new List<HttpResponseMessage>();

        try
        {
            for (var attempt = 1; attempt <= 11; attempt++)
            {
                var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/Account/Login");
                responses.Add(await client.PostAsync(
                    "/Account/Login",
                    Form(
                        token,
                        ("Email", $"missing-{attempt}@documents.demo"),
                        ("Password", "IncorrectPassword123!"))));
            }

            Assert.All(responses.Take(10), response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(HttpStatusCode.TooManyRequests, responses[10].StatusCode);
            Assert.Equal("60", Assert.Single(responses[10].Headers.GetValues("Retry-After")));
            Assert.Contains(
                "Too many requests",
                await responses[10].Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task GlobalAntiforgeryAndLoginValidationLimitsRejectBadInput()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);

        var missingToken = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = "viewer@documents.demo",
                ["Password"] = DemoAccounts.SharedPassword,
            }));
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/Account/Login");
        var oversized = await client.PostAsync(
            "/Account/Login",
            Form(
                token,
                ("Email", $"{new string('x', 260)}@documents.demo"),
                ("Password", DemoAccounts.SharedPassword)));
        var html = await oversized.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, oversized.StatusCode);
        Assert.Contains("maximum length of 254", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LayoutProvidesKeyboardNavigationAndCurrentPageSemantics()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = CreateClient(factory);
        await SignInAsync(client, "manager@documents.demo", DemoAccounts.SharedPassword);

        var html = await client.GetStringAsync("/");

        Assert.Contains("class=\"skip-link\" href=\"#main-content\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"main-content\" tabindex=\"-1\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"primary-sidebar\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"menu\"", html, StringComparison.Ordinal);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Contains(
            "default-src 'self'",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")),
            StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("same-origin", Assert.Single(response.Headers.GetValues("Cross-Origin-Opener-Policy")));
        Assert.Contains(
            "camera=()",
            Assert.Single(response.Headers.GetValues("Permissions-Policy")),
            StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    private static async Task SignInAsync(HttpClient client, string email, string password)
    {
        var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/Account/Login");
        var response = await client.PostAsync(
            "/Account/Login",
            Form(token, ("Email", email), ("Password", password)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static FormUrlEncodedContent Form(
        string token,
        params (string Key, string Value)[] fields)
    {
        var values = fields.ToDictionary(field => field.Key, field => field.Value);
        values["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(values);
    }

    private sealed class ThrowingDashboardQuery : IDashboardQuery
    {
        public const string SensitiveMessage =
            "Confidential diagnostic detail that must never appear in a response.";

        public Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<DashboardSnapshot>(new InvalidOperationException(SensitiveMessage));
    }
}
