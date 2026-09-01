using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Application.Dashboard;
using DocumentLifecycle.Application.Documents;
using DocumentLifecycle.Application.Files;
using DocumentLifecycle.Application.ReferenceData;
using DocumentLifecycle.Infrastructure.Dashboard;
using DocumentLifecycle.Infrastructure.Documents;
using DocumentLifecycle.Infrastructure.Files;
using DocumentLifecycle.Infrastructure.Identity;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.ReferenceData;
using DocumentLifecycle.Infrastructure.Time;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DemoModeOptions>()
            .Bind(configuration.GetSection(DemoModeOptions.SectionName))
            .Validate(options => options.SeedVersion > 0, "Demo seed version must be positive.")
            .Validate(
                options => options.ActivityWriteIntervalMinutes > 0,
                "Demo activity write interval must be positive.")
            .Validate(
                options => options.CleanupIntervalMinutes > 0,
                "Demo cleanup interval must be positive.")
            .Validate(options => options.CookieLifetimeDays > 0, "Demo cookie lifetime must be positive.")
            .ValidateOnStart();
        services
            .AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.RootPath), "A file storage root is required.")
            .Validate(
                options => options.MaximumFileSizeBytes is > 0 and <= 10 * 1024 * 1024,
                "The maximum file size must be between 1 byte and 10 MB.")
            .ValidateOnStart();

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var isSqlite = string.Equals(
                configuration["Database:Provider"],
                "Sqlite",
                StringComparison.OrdinalIgnoreCase);
            var connectionString = (isSqlite
                    ? configuration["Database:SqliteConnection"]
                    : configuration.GetConnectionString("DefaultConnection"))
                ?? throw new InvalidOperationException("A database connection string is required.");

            if (isSqlite)
            {
                options.UseSqlite(connectionString);
                return;
            }

            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 46)),
                mysql => mysql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        });

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "DocumentLifecycle.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(2);
        });

        services.AddScoped<DemoIdentitySeeder>();
        services.AddScoped<IDashboardQuery, DashboardQuery>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentFileService, DocumentFileService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<CurrentWorkspace>();
        services.AddScoped<ICurrentWorkspace>(provider => provider.GetRequiredService<CurrentWorkspace>());
        services.AddSingleton<WorkspaceUploadPathResolver>();
        services.AddScoped<IWorkspaceSeedService, WorkspaceSeedService>();
        services.AddScoped<IWorkspaceFileCleaner, WorkspaceFileCleaner>();
        services.AddScoped<WorkspaceCoordinator>();
        services.AddScoped<WorkspaceCleanupRunner>();
        services.AddHostedService<WorkspaceCleanupHostedService>();

        return services;
    }
}
