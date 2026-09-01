using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Documents;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Web.ViewModels.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewDashboard)]
public sealed class DocumentsController(
    IDocumentService documents,
    IClock clock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] DocumentListQueryViewModel query,
        CancellationToken cancellationToken)
    {
        var results = await documents.GetListAsync(query.ToFilter(), CanManage, cancellationToken);
        var options = await documents.GetFormOptionsAsync(cancellationToken);
        return View(new DocumentListViewModel(query, results, options, CanManage));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetDetailsAsync(id, CanManage, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        ViewData["CanManageDocuments"] = CanManage;
        return View(document);
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var options = await documents.GetFormOptionsAsync(cancellationToken);
        return View(new DocumentFormPageViewModel(
            new DocumentFormViewModel
            {
                EffectiveDate = DateOnly.FromDateTime(clock.UtcNow),
            },
            options));
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = nameof(DocumentFormPageViewModel.Form))] DocumentFormViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(new DocumentFormPageViewModel(
                form,
                await documents.GetFormOptionsAsync(cancellationToken)));
        }

        var result = await documents.CreateDraftAsync(form.ToInput(), Actor, cancellationToken);
        if (result.Status == DocumentMutationStatus.Rejected)
        {
            ModelState.AddModelError(string.Empty, result.Message!);
            return View(new DocumentFormPageViewModel(
                form,
                await documents.GetFormOptionsAsync(cancellationToken)));
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.PublicId });
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetDraftAsync(id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        return View(new DocumentFormPageViewModel(
            new DocumentFormViewModel
            {
                Title = document.Title,
                Description = document.Description,
                CategoryId = document.CategoryId,
                OwnerId = document.OwnerId,
                EffectiveDate = document.EffectiveDate,
                ExpiryDate = document.ExpiryDate,
            },
            await documents.GetFormOptionsAsync(cancellationToken),
            document.PublicId,
            document.Code));
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        [Bind(Prefix = nameof(DocumentFormPageViewModel.Form))] DocumentFormViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(new DocumentFormPageViewModel(
                form,
                await documents.GetFormOptionsAsync(cancellationToken),
                id));
        }

        var result = await documents.UpdateDraftAsync(id, form.ToInput(), Actor, cancellationToken);
        if (result.Status == DocumentMutationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == DocumentMutationStatus.Rejected)
        {
            ModelState.AddModelError(string.Empty, result.Message!);
            return View(new DocumentFormPageViewModel(
                form,
                await documents.GetFormOptionsAsync(cancellationToken),
                id));
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await documents.ActivateAsync(id, Actor, cancellationToken);
        if (result.Status == DocumentMutationStatus.NotFound)
        {
            return NotFound();
        }

        TempData[result.Status == DocumentMutationStatus.Rejected ? "ErrorMessage" : "StatusMessage"] =
            result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool CanManage => User.IsInRole(ApplicationRoles.Administrator) ||
        User.IsInRole(ApplicationRoles.DocumentManager);

    private string Actor => User.Identity?.Name ?? "demo-user";
}
