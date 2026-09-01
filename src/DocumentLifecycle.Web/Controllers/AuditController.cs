using DocumentLifecycle.Application.Audit;
using DocumentLifecycle.Application.Security;
using DocumentLifecycle.Web.ViewModels.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentLifecycle.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewAudit)]
public sealed class AuditController(IAuditQuery auditQuery) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] AuditListQueryViewModel query,
        CancellationToken cancellationToken)
    {
        var results = await auditQuery.GetAsync(query.ToFilter(), cancellationToken);
        return View(new AuditListViewModel(query, results));
    }
}
