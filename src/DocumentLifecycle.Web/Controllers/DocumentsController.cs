using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Documents;
using DocumentLifecycle.Application.Files;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Web.ViewModels.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewDashboard)]
public sealed class DocumentsController(
    IDocumentService documents,
    IDocumentFileService documentFiles,
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

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpGet]
    public async Task<IActionResult> UploadRevision(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetDetailsAsync(id, includeDrafts: true, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (document.State == LifecycleState.Archived)
        {
            TempData["ErrorMessage"] = "Archived documents cannot receive a revision.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(new RevisionUploadPageViewModel(document, new RevisionUploadViewModel()));
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadRevision(
        Guid id,
        [Bind(Prefix = nameof(RevisionUploadPageViewModel.Form))] RevisionUploadViewModel form,
        CancellationToken cancellationToken)
    {
        var document = await documents.GetDetailsAsync(id, includeDrafts: true, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || form.Upload is null)
        {
            return View(new RevisionUploadPageViewModel(document, form));
        }

        await using var content = form.Upload.OpenReadStream();
        var result = await documentFiles.UploadRevisionAsync(
            id,
            new RevisionUploadInput(
                form.ChangeNote,
                form.Upload.FileName,
                form.Upload.ContentType,
                form.Upload.Length,
                content),
            Actor,
            cancellationToken);
        if (result.Status == DocumentFileMutationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == DocumentFileMutationStatus.Rejected)
        {
            ModelState.AddModelError(string.Empty, result.Message!);
            return View(new RevisionUploadPageViewModel(document, form with { Upload = null }));
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Download(
        Guid id,
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        var download = await documentFiles.GetDownloadAsync(
            id,
            revisionId,
            allowDraft: CanManage,
            cancellationToken);
        if (download is null)
        {
            return NotFound();
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ETag = $"\"sha256-{download.Sha256Hash}\"";
        return File(
            download.Content,
            download.MediaType,
            download.DownloadFilename,
            enableRangeProcessing: true);
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpGet]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetDetailsAsync(id, includeDrafts: true, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (document.State != LifecycleState.Active)
        {
            TempData["ErrorMessage"] = "Only active documents can be archived.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(new ArchiveDocumentPageViewModel(
            document,
            new ArchiveDocumentFormViewModel()));
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(
        Guid id,
        [Bind(Prefix = nameof(ArchiveDocumentPageViewModel.Form))] ArchiveDocumentFormViewModel form,
        CancellationToken cancellationToken)
    {
        var document = await documents.GetDetailsAsync(id, includeDrafts: true, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(new ArchiveDocumentPageViewModel(document, form));
        }

        var result = await documents.ArchiveAsync(id, form.Reason, Actor, cancellationToken);
        if (result.Status == DocumentMutationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == DocumentMutationStatus.Rejected)
        {
            ModelState.AddModelError(string.Empty, result.Message!);
            return View(new ArchiveDocumentPageViewModel(document, form));
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = AuthorizationPolicies.ManageDocuments)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var result = await documents.RestoreAsync(id, Actor, cancellationToken);
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
