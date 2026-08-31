namespace DocumentLifecycle.Web.Workspaces;

internal static class WorkspaceApplicationBuilderExtensions
{
    public static IApplicationBuilder UseDemoWorkspace(this IApplicationBuilder app) =>
        app.UseMiddleware<DemoWorkspaceMiddleware>();
}
