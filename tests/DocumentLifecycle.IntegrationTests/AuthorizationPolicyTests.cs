using System.Security.Claims;
using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class AuthorizationPolicyTests(DocumentLifecycleWebApplicationFactory factory)
    : IClassFixture<DocumentLifecycleWebApplicationFactory>
{
    [Theory]
    [InlineData(ApplicationRoles.Administrator, AuthorizationPolicies.ManageConfiguration, true)]
    [InlineData(ApplicationRoles.DocumentManager, AuthorizationPolicies.ManageConfiguration, false)]
    [InlineData(ApplicationRoles.Viewer, AuthorizationPolicies.ManageConfiguration, false)]
    [InlineData(ApplicationRoles.Administrator, AuthorizationPolicies.ManageDocuments, true)]
    [InlineData(ApplicationRoles.DocumentManager, AuthorizationPolicies.ManageDocuments, true)]
    [InlineData(ApplicationRoles.Viewer, AuthorizationPolicies.ManageDocuments, false)]
    [InlineData(ApplicationRoles.Administrator, AuthorizationPolicies.ViewAudit, true)]
    [InlineData(ApplicationRoles.DocumentManager, AuthorizationPolicies.ViewAudit, false)]
    [InlineData(ApplicationRoles.Viewer, AuthorizationPolicies.ViewAudit, true)]
    public async Task RolePolicyMatchesAuthorizationMatrix(
        string role,
        string policy,
        bool expected)
    {
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "policy-test@documents.demo"),
            new Claim(ClaimTypes.Role, role),
        ], "PolicyTest"));

        var result = await authorization.AuthorizeAsync(principal, resource: null, policy);

        Assert.Equal(expected, result.Succeeded);
    }
}
