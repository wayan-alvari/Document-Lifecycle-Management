using System.ComponentModel.DataAnnotations;
using DocumentLifecycle.Application.Audit;

namespace DocumentLifecycle.Web.ViewModels.Audit;

public sealed record AuditListQueryViewModel
{
    [StringLength(200)]
    public string? Search { get; init; }

    [StringLength(100)]
    public string? EventAction { get; init; }

    public int Page { get; init; } = 1;

    public AuditListFilter ToFilter() => new(Search, EventAction, Page);
}

public sealed record AuditListViewModel(
    AuditListQueryViewModel Query,
    AuditListPage Results);
