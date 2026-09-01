using DocumentLifecycle.Infrastructure.Identity;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Web;

internal static class DemoIdentityInitialization
{
    public static async Task InitializeDemoIdentityAsync(
        this WebApplication app,
        bool explicitlyRequested = false)
    {
        if (!app.Configuration.GetValue<bool>("DemoMode:Enabled"))
        {
            if (explicitlyRequested)
            {
                throw new InvalidOperationException(
                    "Explicit demo initialization requires DemoMode:Enabled=true.");
            }

            return;
        }

        if (!explicitlyRequested &&
            !app.Environment.IsDevelopment() &&
            !app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (database.Database.IsSqlite())
        {
            await database.Database.EnsureCreatedAsync();
        }
        else
        {
            await database.Database.MigrateAsync();
        }

        var seeder = scope.ServiceProvider.GetRequiredService<DemoIdentitySeeder>();
        await seeder.SeedAsync();
        app.Logger.LogInformation(
            explicitlyRequested
                ? "Explicit demo database initialization completed."
                : "Development demo database initialization completed.");
    }
}
