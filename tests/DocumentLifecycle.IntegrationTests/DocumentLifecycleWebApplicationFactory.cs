using DocumentLifecycle.Application.Abstractions.Time;
using DocumentLifecycle.IntegrationTests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocumentLifecycle.IntegrationTests;

public sealed class DocumentLifecycleWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"document-lifecycle-auth-{Guid.NewGuid():N}.db");

    public TestClock Clock { get; } = new();

    public string UploadRoot { get; } = Path.Combine(
        Path.GetTempPath(),
        $"document-lifecycle-uploads-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:SqliteConnection"] = $"Data Source={databasePath};Pooling=False",
                ["Database:Provider"] = "Sqlite",
                ["DemoMode:Enabled"] = "true",
                ["FileStorage:RootPath"] = UploadRoot,
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(databasePath))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }

        if (disposing && Directory.Exists(UploadRoot))
        {
            Directory.Delete(UploadRoot, recursive: true);
        }
    }
}
