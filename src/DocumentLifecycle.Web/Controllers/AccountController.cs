using DocumentLifecycle.Infrastructure.Identity;
using DocumentLifecycle.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocumentLifecycle.Web.Controllers;

public sealed class AccountController(
    SignInManager<ApplicationUser> signInManager,
    ILogger<AccountController> logger) : Controller
{
    private static readonly EventId LoginSucceeded = new(1001, nameof(LoginSucceeded));
    private static readonly EventId LoginRejected = new(1002, nameof(LoginRejected));
    private static readonly EventId SignedOut = new(1003, nameof(SignedOut));

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool switched = false)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["Switched"] = switched;
        return View(new LoginViewModel { ReturnUrl = LocalReturnUrl(returnUrl) });
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model with { ReturnUrl = LocalReturnUrl(model.ReturnUrl) });
        }

        var result = await signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            logger.LogInformation(LoginSucceeded, "A demo account signed in successfully.");
            return LocalRedirect(LocalReturnUrl(model.ReturnUrl) ?? Url.Action("Index", "Home")!);
        }

        var outcome = result.IsLockedOut ? "locked-out" : "invalid-credentials";
        logger.LogWarning(LoginRejected, "A demo sign-in was rejected with outcome {Outcome}.", outcome);
        ModelState.AddModelError(
            string.Empty,
            result.IsLockedOut
                ? "Sign-in is temporarily locked. Please wait five minutes and try again."
                : "The email address or password is not valid.");

        return View(model with { Password = string.Empty, ReturnUrl = LocalReturnUrl(model.ReturnUrl) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        logger.LogInformation(SignedOut, "A demo account signed out.");
        return RedirectToAction(nameof(Login), new { switched = true });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    private string? LocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
}
