using System.ComponentModel.DataAnnotations;
using DocumentLifecycle.Application.Documents;

namespace DocumentLifecycle.Web.ViewModels.Documents;

public sealed record ArchiveDocumentFormViewModel
{
    [Required]
    [StringLength(500)]
    [Display(Name = "Archive reason")]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ArchiveDocumentPageViewModel(
    DocumentDetails Document,
    ArchiveDocumentFormViewModel Form);
