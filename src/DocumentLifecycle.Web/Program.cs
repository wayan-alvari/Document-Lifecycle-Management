using DocumentLifecycle.Infrastructure;
using DocumentLifecycle.Web;
using DocumentLifecycle.Web.Middleware;
using DocumentLifecycle.Web.Workspaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.MaxModelValidationErrors = 50;
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationAuthorization();
builder.Services.AddSingleton<WorkspaceCookieService>();
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 200;
    options.KeyLengthLimit = 200;
    options.ValueLengthLimit = 2 * 1024 * 1024;
    options.MultipartBodyLengthLimit = 11 * 1024 * 1024;
    options.MultipartHeadersLengthLimit = 16 * 1024;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 11 * 1024 * 1024;
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ||
        builder.Environment.IsEnvironment("Testing")
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});

var app = builder.Build();

await app.InitializeDemoIdentityAsync();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/StatusPage", "?code={0}");

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseDemoWorkspace();
app.UseAuthorization();

app.MapGet("/health", (HttpContext context) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Text("Healthy", "text/plain");
    })
    .WithName("Liveness")
    .AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapFallback(() => Results.NotFound())
    .AllowAnonymous();

app.Run();

public partial class Program;
