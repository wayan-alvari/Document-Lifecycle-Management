using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace DocumentLifecycle.Web;

internal static class AuthorizationConfiguration
{
    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(
                AuthorizationPolicies.ViewDashboard,
                policy => policy.RequireRole(ApplicationRoles.All));

            options.AddPolicy(
                AuthorizationPolicies.ManageDocuments,
                policy => policy.RequireRole(
                    ApplicationRoles.Administrator,
                    ApplicationRoles.DocumentManager));

            options.AddPolicy(
                AuthorizationPolicies.ManageConfiguration,
                policy => policy.RequireRole(ApplicationRoles.Administrator));

            options.AddPolicy(
                AuthorizationPolicies.ViewAudit,
                policy => policy.RequireRole(
                    ApplicationRoles.Administrator,
                    ApplicationRoles.Viewer));

            options.AddPolicy(
                AuthorizationPolicies.ExportDocuments,
                policy => policy.RequireRole(ApplicationRoles.All));
        });

        return services;
    }
}
