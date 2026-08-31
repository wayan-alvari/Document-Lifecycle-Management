namespace DocumentLifecycle.Application.Security;

public static class AuthorizationPolicies
{
    public const string ViewDashboard = nameof(ViewDashboard);
    public const string ManageDocuments = nameof(ManageDocuments);
    public const string ManageConfiguration = nameof(ManageConfiguration);
    public const string ViewAudit = nameof(ViewAudit);
    public const string ExportDocuments = nameof(ExportDocuments);
}
