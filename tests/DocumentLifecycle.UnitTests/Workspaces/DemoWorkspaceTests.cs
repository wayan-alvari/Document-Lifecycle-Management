using DocumentLifecycle.Domain.Workspaces;

namespace DocumentLifecycle.UnitTests.Workspaces;

public sealed class DemoWorkspaceTests
{
    private static readonly DateTime Start = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WorkspaceExpiresAtExactlySixHoursOfInactivity()
    {
        var workspace = DemoWorkspace.Create(Guid.NewGuid(), Start, seedVersion: 1);

        Assert.False(workspace.IsExpired(Start.AddHours(6).AddTicks(-1)));
        Assert.True(workspace.IsExpired(Start.AddHours(6)));
    }

    [Fact]
    public void MeaningfulActivityIsThrottledAndExtendsExpiry()
    {
        var workspace = DemoWorkspace.Create(Guid.NewGuid(), Start, seedVersion: 1);

        Assert.False(workspace.RecordMeaningfulActivity(Start.AddMinutes(4), TimeSpan.FromMinutes(5)));
        Assert.Equal(Start, workspace.LastActivityAtUtc);

        Assert.True(workspace.RecordMeaningfulActivity(Start.AddMinutes(5), TimeSpan.FromMinutes(5)));
        Assert.Equal(Start.AddMinutes(5), workspace.LastActivityAtUtc);
        Assert.Equal(Start.AddHours(6).AddMinutes(5), workspace.ExpiresAtUtc);
        Assert.Equal(1, workspace.Version);
    }

    [Fact]
    public void WorkspaceRejectsNonUtcTimestamps()
    {
        var localTime = DateTime.SpecifyKind(Start, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => DemoWorkspace.Create(Guid.NewGuid(), localTime, seedVersion: 1));
    }
}
