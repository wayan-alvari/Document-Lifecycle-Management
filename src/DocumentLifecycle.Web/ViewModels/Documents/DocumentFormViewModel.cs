using System.ComponentModel.DataAnnotations;
using DocumentLifecycle.Application.Documents;

namespace DocumentLifecycle.Web.ViewModels.Documents;

public sealed record DocumentFormViewModel : IValidatableObject
{
    [Required]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; init; } = string.Empty;

    [Required(ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public Guid? CategoryId { get; init; }

    [Required(ErrorMessage = "Choose an owner.")]
    [Display(Name = "Owner")]
    public Guid? OwnerId { get; init; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Effective date")]
    public DateOnly? EffectiveDate { get; init; }

    [DataType(DataType.Date)]
    [Display(Name = "Expiry or review date")]
    public DateOnly? ExpiryDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveDate is not null && ExpiryDate is not null && ExpiryDate < EffectiveDate)
        {
            yield return new ValidationResult(
                "Expiry date cannot precede the effective date.",
                [nameof(ExpiryDate)]);
        }
    }

    public DocumentDraftInput ToInput() => new(
        Title,
        Description,
        CategoryId!.Value,
        OwnerId!.Value,
        EffectiveDate!.Value,
        ExpiryDate);
}

public sealed record DocumentFormPageViewModel(
    DocumentFormViewModel Form,
    DocumentFormOptions Options,
    Guid? PublicId = null,
    string? Code = null);
