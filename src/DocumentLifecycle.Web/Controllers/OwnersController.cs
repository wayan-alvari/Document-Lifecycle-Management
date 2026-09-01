using DocumentLifecycle.Application.ReferenceData;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Web.ViewModels.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ManageConfiguration)]
public sealed class OwnersController(IReferenceDataService referenceData) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owners = await referenceData.GetOwnersAsync(cancellationToken);
        return View(owners);
    }

    [HttpGet]
    public IActionResult Create() => View(new OwnerFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        OwnerFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await referenceData.CreateOwnerAsync(
            model.DisplayName,
            model.Contact,
            Actor,
            cancellationToken);
        if (result.Status == ReferenceMutationStatus.Rejected)
        {
            ModelState.AddModelError(nameof(model.DisplayName), result.Message!);
            return View(model);
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var owner = await referenceData.GetOwnerAsync(id, cancellationToken);
        if (owner is null)
        {
            return NotFound();
        }

        ViewData["PublicId"] = owner.PublicId;
        return View(new OwnerFormViewModel
        {
            DisplayName = owner.DisplayName,
            Contact = owner.Contact,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        OwnerFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["PublicId"] = id;
            return View(model);
        }

        var result = await referenceData.UpdateOwnerAsync(
            id,
            model.DisplayName,
            model.Contact,
            Actor,
            cancellationToken);
        if (result.Status == ReferenceMutationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == ReferenceMutationStatus.Rejected)
        {
            ModelState.AddModelError(nameof(model.DisplayName), result.Message!);
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
        var result = await referenceData.ToggleOwnerAsync(id, Actor, cancellationToken);
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
        var owner = await referenceData.GetOwnerAsync(id, cancellationToken);
        return owner is null ? NotFound() : View(owner);
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var result = await referenceData.DeleteOwnerAsync(id, Actor, cancellationToken);
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
