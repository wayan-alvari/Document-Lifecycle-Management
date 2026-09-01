using DocumentLifecycle.Application.ReferenceData;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Web.ViewModels.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ManageConfiguration)]
public sealed class CategoriesController(IReferenceDataService referenceData) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await referenceData.GetCategoriesAsync(cancellationToken);
        return View(categories);
    }

    [HttpGet]
    public IActionResult Create() => View(new CategoryFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CategoryFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await referenceData.CreateCategoryAsync(
            model.Name,
            model.Description,
            Actor,
            cancellationToken);
        if (result.Status == ReferenceMutationStatus.Rejected)
        {
            ModelState.AddModelError(nameof(model.Name), result.Message!);
            return View(model);
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var category = await referenceData.GetCategoryAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        ViewData["PublicId"] = category.PublicId;
        ViewData["IsActive"] = category.IsActive;
        return View(new CategoryFormViewModel
        {
            Name = category.Name,
            Description = category.Description,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        CategoryFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["PublicId"] = id;
            return View(model);
        }

        var result = await referenceData.UpdateCategoryAsync(
            id,
            model.Name,
            model.Description,
            Actor,
            cancellationToken);
        if (result.Status == ReferenceMutationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == ReferenceMutationStatus.Rejected)
        {
            ModelState.AddModelError(nameof(model.Name), result.Message!);
            ViewData["PublicId"] = id;
            return View(model);
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken cancellationToken)
    {
        var result = await referenceData.ToggleCategoryAsync(id, Actor, cancellationToken);
        if (result.Status == ReferenceMutationStatus.NotFound)
        {
            return NotFound();
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var category = await referenceData.GetCategoryAsync(id, cancellationToken);
        return category is null ? NotFound() : View(category);
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var result = await referenceData.DeleteCategoryAsync(id, Actor, cancellationToken);
        if (result.Status == ReferenceMutationStatus.NotFound)
        {
            return NotFound();
        }

        TempData[result.Status == ReferenceMutationStatus.Rejected ? "ErrorMessage" : "StatusMessage"] =
            result.Message;
        return RedirectToAction(nameof(Index));
    }

    private string Actor => User.Identity?.Name ?? "demo-user";
}
