using System.ComponentModel.DataAnnotations;

namespace DocumentLifecycle.Web.ViewModels.Account;

public sealed record LoginViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email address")]
    public string Email { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = string.Empty;

    public string? ReturnUrl { get; init; }
}
