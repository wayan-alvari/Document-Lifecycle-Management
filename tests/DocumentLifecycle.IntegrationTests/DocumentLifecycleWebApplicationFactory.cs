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
    private readonly SqliteConnection databaseConnection = new(
        $"Data Source=document-lifecycle-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False");

    public DocumentLifecycleWebApplicationFactory()
    {
        databaseConnection.Open();
    }

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
                ["Database:SqliteConnection"] = databaseConnection.ConnectionString,
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

        if (disposing)
        {
            databaseConnection.Dispose();
        }

        if (disposing && Directory.Exists(UploadRoot))
        {
            Directory.Delete(UploadRoot, recursive: true);
        }
    }
}
