using System.ComponentModel.DataAnnotations;
using DocumentLifecycle.Application.Documents;

namespace DocumentLifecycle.Web.ViewModels.Documents;

public sealed record DocumentListQueryViewModel
{
    [StringLength(200)]
    public string? Search { get; init; }

    public DocumentListStatus Status { get; init; } = DocumentListStatus.All;

    public Guid? CategoryId { get; init; }

    public Guid? OwnerId { get; init; }

    [DataType(DataType.Date)]
    public DateOnly? ExpiryFrom { get; init; }

    [DataType(DataType.Date)]
    public DateOnly? ExpiryTo { get; init; }

    public int Page { get; init; } = 1;

    public DocumentListFilter ToFilter() => new(
        Search,
        Status,
        CategoryId,
        OwnerId,
        ExpiryFrom,
        ExpiryTo,
        Page);
}

public sealed record DocumentListViewModel(
    DocumentListQueryViewModel Query,
    DocumentListPage Results,
    DocumentFormOptions Options,
    bool CanManageDocuments);
