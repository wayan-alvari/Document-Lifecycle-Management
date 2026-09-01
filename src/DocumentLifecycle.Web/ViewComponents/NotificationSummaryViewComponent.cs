using DocumentLifecycle.Application.Notifications;
using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.ViewComponents;

public sealed class NotificationSummaryViewComponent(INotificationService notifications) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var cancellationToken = HttpContext.RequestAborted;
        var role = ApplicationRoles.All.FirstOrDefault(User.IsInRole);
        if (role is null)
        {
            return Content(string.Empty);
        }

        await notifications.RefreshExpiryNotificationsAsync(cancellationToken);
        var unreadCount = await notifications.GetUnreadCountAsync(role, cancellationToken);
        return View(unreadCount);
    }
}
