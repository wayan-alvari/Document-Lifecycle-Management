using DocumentLifecycle.Infrastructure.Identity;
using DocumentLifecycle.Infrastructure.Persistence;
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

        return services;
    }
}
