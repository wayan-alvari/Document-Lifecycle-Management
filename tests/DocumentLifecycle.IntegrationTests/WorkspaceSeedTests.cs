using DocumentLifecycle.Application.Abstractions.Workspaces;
using DocumentLifecycle.Domain.Documents;
using DocumentLifecycle.Infrastructure.Persistence;
using DocumentLifecycle.Infrastructure.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentLifecycle.IntegrationTests;

public sealed class WorkspaceSeedTests
{
    [Fact]
    public async Task NewWorkspaceReceivesIdempotentFictionalLifecycleDataset()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/Account/Login");
        var workspaceId = GetWorkspaceId(response);

        await using var scope = factory.Services.CreateAsyncScope();
        var currentWorkspace = scope.ServiceProvider.GetRequiredService<CurrentWorkspace>();
        currentWorkspace.Set(workspaceId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(4, await database.DocumentCategories.CountAsync());
        Assert.Equal(3, await database.DocumentOwners.CountAsync());
        Assert.Equal(12, await database.ManagedDocuments.CountAsync());
        Assert.Equal(11, await database.DocumentRevisions.CountAsync());
        Assert.Equal(35, await database.AuditEvents.CountAsync());
        Assert.Equal(2, await database.ManagedDocuments.CountAsync(document => document.State == LifecycleState.Draft));
        Assert.Equal(8, await database.ManagedDocuments.CountAsync(document => document.State == LifecycleState.Active));
        Assert.Equal(2, await database.ManagedDocuments.CountAsync(document => document.State == LifecycleState.Archived));

        var today = DateOnly.FromDateTime(factory.Clock.UtcNow);
        var statuses = (await database.ManagedDocuments.AsNoTracking().ToListAsync())
            .GroupBy(document => document.GetDisplayStatus(today))
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(2, statuses[DocumentDisplayStatus.Draft]);
        Assert.Equal(3, statuses[DocumentDisplayStatus.Active]);
        Assert.Equal(3, statuses[DocumentDisplayStatus.ExpiringSoon]);
        Assert.Equal(2, statuses[DocumentDisplayStatus.Expired]);
        Assert.Equal(2, statuses[DocumentDisplayStatus.Archived]);

        var filesBefore = Directory.GetFiles(Path.Combine(factory.UploadRoot, workspaceId.ToString("N")));
        Assert.Equal(11, filesBefore.Length);
        Assert.All(filesBefore, file => Assert.StartsWith("%PDF-1.4", File.ReadAllText(file)));

        var seeder = scope.ServiceProvider.GetRequiredService<IWorkspaceSeedService>();
        await seeder.SeedAsync(workspaceId);

        Assert.Equal(12, await database.ManagedDocuments.CountAsync());
        Assert.Equal(35, await database.AuditEvents.CountAsync());
        Assert.Equal(filesBefore.Order(), Directory.GetFiles(Path.Combine(factory.UploadRoot, workspaceId.ToString("N"))).Order());
    }

    [Fact]
    public async Task QueryFiltersAndUniqueCodesEnforceWorkspaceIsolation()
    {
        await using var factory = new DocumentLifecycleWebApplicationFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstId = GetWorkspaceId(await firstClient.GetAsync("/Account/Login"));
        var secondId = GetWorkspaceId(await secondClient.GetAsync("/Account/Login"));

        await using var scope = factory.Services.CreateAsyncScope();
        var currentWorkspace = scope.ServiceProvider.GetRequiredService<CurrentWorkspace>();
        currentWorkspace.Set(firstId);
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(12, await database.ManagedDocuments.CountAsync());
        Assert.False(await database.ManagedDocuments.AnyAsync(document => document.WorkspaceId == secondId));
        Assert.Equal(24, await database.ManagedDocuments.IgnoreQueryFilters().CountAsync());
        Assert.Equal(
            2,
            await database.ManagedDocuments.IgnoreQueryFilters().CountAsync(document => document.Code == "DOC-0001"));

        var categoryId = await database.DocumentCategories.Select(category => category.Id).FirstAsync();
        var ownerId = await database.DocumentOwners.Select(owner => owner.Id).FirstAsync();
        database.ManagedDocuments.Add(ManagedDocument.CreateDraft(
            firstId,
            "DOC-0001",
            "Duplicate code",
            "Synthetic uniqueness test",
            categoryId,
            ownerId,
            DateOnly.FromDateTime(factory.Clock.UtcNow),
            null,
            "test",
            factory.Clock.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }

    private static Guid GetWorkspaceId(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("X-Demo-Workspace", out var values));
        return Guid.ParseExact(Assert.Single(values), "N");
    }
}
