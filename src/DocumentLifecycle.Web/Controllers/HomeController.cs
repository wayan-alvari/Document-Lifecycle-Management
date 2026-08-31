using DocumentLifecycle.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.Controllers;

public sealed class HomeController : Controller
{
    [Authorize(Policy = AuthorizationPolicies.ViewDashboard)]
    public IActionResult Index()
    {
        return View();
    }
}
