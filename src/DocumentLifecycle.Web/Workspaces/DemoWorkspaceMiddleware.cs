using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.Extensions.Options;

namespace DocumentLifecycle.Web.Workspaces;

internal sealed class DemoWorkspaceMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(
        HttpContext context,
        WorkspaceCookieService cookieService,
        WorkspaceCoordinator coordinator,
        IClock clock,
        IOptions<DemoModeOptions> options)
    {
        if (!options.Value.Enabled || IsIgnoredPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var hasValidCookie = cookieService.TryRead(context.Request, out var workspaceId);
        var resolution = await coordinator.ResolveAsync(
            hasValidCookie ? workspaceId : null,
            IsMeaningfulActivity(context),
            context.RequestAborted);

        if (!hasValidCookie || resolution.WorkspaceId != workspaceId)
        {
            context.Response.Cookies.Append(
                DemoWorkspaceCookie.Name,
                cookieService.Protect(resolution.WorkspaceId),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing"),
                    Path = "/",
                    MaxAge = TimeSpan.FromDays(options.Value.CookieLifetimeDays),
                });
        }

        context.Response.Headers.Append("X-Demo-Workspace", resolution.WorkspaceId.ToString("N"));
        await next(context);
    }

    private static bool IsIgnoredPath(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/notifications/poll");

    private static bool IsMeaningfulActivity(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (HttpMethods.IsPost(context.Request.Method) ||
            HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method) ||
            HttpMethods.IsDelete(context.Request.Method))
        {
            return true;
        }

        return HttpMethods.IsGet(context.Request.Method) &&
            !string.Equals(
                context.Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase) &&
            context.Request.GetTypedHeaders().Accept?.Any(mediaType =>
                string.Equals(
                    mediaType.MediaType.Value,
                    "text/html",
                    StringComparison.OrdinalIgnoreCase)) == true;
    }
}
