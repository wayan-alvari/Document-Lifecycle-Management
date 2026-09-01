using System.ComponentModel.DataAnnotations;

namespace DocumentLifecycle.Web.ViewModels.ReferenceData;

public sealed record CategoryFormViewModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string Description { get; init; } = string.Empty;
}
