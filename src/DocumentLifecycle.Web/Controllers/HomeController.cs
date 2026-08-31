using DocumentLifecycle.Application.Dashboard;
using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.Controllers;

public sealed class HomeController(IDashboardQuery dashboardQuery) : Controller
{
    [Authorize(Policy = AuthorizationPolicies.ViewDashboard)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await dashboardQuery.GetAsync(cancellationToken);
        return View(dashboard);
    }
}
