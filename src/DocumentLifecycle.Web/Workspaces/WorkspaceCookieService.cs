using Microsoft.AspNetCore.DataProtection;

namespace DocumentLifecycle.Web.Workspaces;

internal sealed class WorkspaceCookieService(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
        "DocumentLifecycle.DemoWorkspace.v1");

    public bool TryRead(HttpRequest request, out Guid workspaceId)
    {
        workspaceId = Guid.Empty;
        if (!request.Cookies.TryGetValue(DemoWorkspaceCookie.Name, out var protectedValue) ||
            string.IsNullOrWhiteSpace(protectedValue))
        {
            return false;
        }

        try
        {
            return Guid.TryParseExact(protector.Unprotect(protectedValue), "N", out workspaceId) &&
                workspaceId != Guid.Empty;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    public string Protect(Guid workspaceId) => protector.Protect(workspaceId.ToString("N"));
}
