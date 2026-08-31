namespace DocumentLifecycle.Application.Security;

public static class ApplicationRoles
{
    public const string Administrator = "Administrator";
    public const string DocumentManager = "Document Manager";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All =
    [
        Administrator,
        DocumentManager,
        Viewer,
    ];
}
