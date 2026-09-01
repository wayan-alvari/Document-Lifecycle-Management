using DocumentLifecycle.Application.Notifications;
using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewDashboard)]
public sealed class NotificationsController(INotificationService notifications) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await notifications.RefreshExpiryNotificationsAsync(cancellationToken);
        return View(await notifications.GetForRoleAsync(CurrentRole, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        if (!await notifications.MarkReadAsync(id, CurrentRole, cancellationToken))
        {
            return NotFound();
        }

        TempData["StatusMessage"] = "Notification marked as read.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var count = await notifications.MarkAllReadAsync(CurrentRole, cancellationToken);
        TempData["StatusMessage"] = count == 0
            ? "There were no unread notifications."
            : $"{count} notification(s) marked as read.";
        return RedirectToAction(nameof(Index));
    }

    private string CurrentRole => ApplicationRoles.All.First(User.IsInRole);
}
