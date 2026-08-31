using DocumentLifecycle.Infrastructure.Identity;
using DocumentLifecycle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentLifecycle.Web;

internal static class DemoIdentityInitialization
{
    public static async Task InitializeDemoIdentityAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("DemoMode:Enabled") ||
            (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            await database.Database.EnsureCreatedAsync();
        }
        else
        {
            await database.Database.MigrateAsync();
        }

        var seeder = scope.ServiceProvider.GetRequiredService<DemoIdentitySeeder>();
        await seeder.SeedAsync();
    }
}
