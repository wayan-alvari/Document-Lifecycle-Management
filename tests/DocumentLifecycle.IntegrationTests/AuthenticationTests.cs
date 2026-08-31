using System.Net;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class AuthenticationTests(DocumentLifecycleWebApplicationFactory factory)
    : IClassFixture<DocumentLifecycleWebApplicationFactory>
{
    [Fact]
    public async Task LoginPageShowsAllDemoAccountsAndResetNotice()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/Account/Login");

        Assert.Contains(
            "Demo data is isolated per browser and automatically resets after 6 hours of inactivity.",
            html,
            StringComparison.Ordinal);
        Assert.Contains("PortfolioDemo123!", html, StringComparison.Ordinal);
        foreach (var account in DemoAccounts.All)
        {
            Assert.Contains(account.Email, html, StringComparison.Ordinal);
            Assert.Contains(account.Role, html, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("admin@documents.demo")]
    [InlineData("manager@documents.demo")]
    [InlineData("viewer@documents.demo")]
    public async Task DemoUserCanSignInAndOpenAuthorizedHome(string email)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var loginResponse = await SignInAsync(client, email, DemoAccounts.SharedPassword);

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/", loginResponse.Headers.Location?.OriginalString);

        var homeResponse = await client.GetAsync("/");
        var homeHtml = await homeResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        Assert.Contains(email, homeHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidPasswordUsesGenericErrorAndDoesNotAuthenticate()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await SignInAsync(client, "viewer@documents.demo", "NotThePassword123!");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The email address or password is not valid.", html, StringComparison.Ordinal);

        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
        Assert.Contains("/Account/Login", homeResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogoutClearsAuthenticationAndOffersRoleSwitch()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var loginResponse = await SignInAsync(client, "manager@documents.demo", DemoAccounts.SharedPassword);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/");
        var logoutResponse = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.StartsWith("/Account/Login", logoutResponse.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
    }

    [Fact]
    public async Task DemoIdentitySeedIsIdempotent()
    {
        _ = factory.Services;
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoIdentitySeeder>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(3, await userManager.Users.CountAsync());
        Assert.Equal(3, await roleManager.Roles.CountAsync());
    }

    private static async Task<HttpResponseMessage> SignInAsync(
        HttpClient client,
        string email,
        string password)
    {
        var token = await AntiforgeryTestHelper.GetTokenAsync(client, "/Account/Login");
        return await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["__RequestVerificationToken"] = token,
            }));
    }
}
