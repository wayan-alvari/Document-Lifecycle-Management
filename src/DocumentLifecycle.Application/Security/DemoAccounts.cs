namespace DocumentLifecycle.Application.Security;

public sealed record DemoAccount(string Role, string Email, string Password, string Capability);

public static class DemoAccounts
{
    public const string SharedPassword = "PortfolioDemo123!";

    public static readonly IReadOnlyList<DemoAccount> All =
    [
        new(
            ApplicationRoles.Administrator,
            "admin@documents.demo",
            SharedPassword,
            "Configuration and all records"),
        new(
            ApplicationRoles.DocumentManager,
            "manager@documents.demo",
            SharedPassword,
            "Create, revise, activate, and archive documents"),
        new(
            ApplicationRoles.Viewer,
            "viewer@documents.demo",
            SharedPassword,
            "Read, search, download, and export"),
    ];
}
