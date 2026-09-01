using System.ComponentModel.DataAnnotations;

namespace DocumentLifecycle.Web.ViewModels.ReferenceData;

public sealed record OwnerFormViewModel
{
    [Required]
    [StringLength(120)]
    [Display(Name = "Display name")]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [StringLength(160)]
    [Display(Name = "Contact or team label")]
    public string Contact { get; init; } = string.Empty;
}
