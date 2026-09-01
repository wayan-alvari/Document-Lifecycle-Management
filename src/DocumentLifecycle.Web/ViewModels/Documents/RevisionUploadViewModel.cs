using System.ComponentModel.DataAnnotations;
using DocumentLifecycle.Application.Documents;

namespace DocumentLifecycle.Web.ViewModels.Documents;

public sealed record RevisionUploadViewModel
{
    [Required]
    [StringLength(500)]
    [Display(Name = "Change note")]
    public string ChangeNote { get; init; } = string.Empty;

    [Required]
    [Display(Name = "Revision file")]
    public IFormFile? Upload { get; init; }
}

public sealed record RevisionUploadPageViewModel(
    DocumentDetails Document,
    RevisionUploadViewModel Form);
