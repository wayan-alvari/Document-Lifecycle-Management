using DocumentLifecycle.Application.Dashboard;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Web.Models;
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

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View(new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier,
        });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusPage(int code)
    {
        var safeCode = code is >= 400 and <= 599 ? code : StatusCodes.Status404NotFound;
        var status = safeCode switch
        {
            StatusCodes.Status404NotFound => new StatusCodeViewModel(
                safeCode,
                "Page not found",
                "The requested page does not exist or is not available in this demo workspace."),
            StatusCodes.Status429TooManyRequests => new StatusCodeViewModel(
                safeCode,
                "Too many requests",
                "Please wait a minute before trying that action again."),
            _ => new StatusCodeViewModel(
                safeCode,
                "Request could not be completed",
                "Return to the workspace and try again."),
        };
        Response.StatusCode = status.StatusCode;
        return View("StatusCode", status);
    }
}
