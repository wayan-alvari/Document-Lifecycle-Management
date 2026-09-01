using System.ComponentModel.DataAnnotations;

namespace DocumentLifecycle.Web.ViewModels.Account;

public sealed record LoginViewModel
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    [Display(Name = "Email address")]
    public string Email { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(128)]
    public string Password { get; init; } = string.Empty;

    [StringLength(2048)]
    public string? ReturnUrl { get; init; }
}
