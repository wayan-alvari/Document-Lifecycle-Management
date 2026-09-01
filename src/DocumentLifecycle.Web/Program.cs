using DocumentLifecycle.Infrastructure;
using DocumentLifecycle.Web;
using DocumentLifecycle.Web.Middleware;
using DocumentLifecycle.Web.Workspaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var initializeDemo = args.Any(argument => string.Equals(
    argument,
    "--initialize-demo",
    StringComparison.OrdinalIgnoreCase));
var applicationArguments = args
    .Where(argument => !string.Equals(argument, "--initialize-demo", StringComparison.OrdinalIgnoreCase))
    .ToArray();
var builder = WebApplication.CreateBuilder(applicationArguments);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    options.UseUtcTimestamp = true;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.MaxModelValidationErrors = 50;
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationAuthorization();
builder.Services.AddSingleton<WorkspaceCookieService>();
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("DocumentLifecycleManagement");
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(dataProtectionKeyPath)));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
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

if (initializeDemo)
{
    await app.InitializeDemoIdentityAsync(explicitlyRequested: true);
    return;
}

await app.InitializeDemoIdentityAsync();

app.UseForwardedHeaders();
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
