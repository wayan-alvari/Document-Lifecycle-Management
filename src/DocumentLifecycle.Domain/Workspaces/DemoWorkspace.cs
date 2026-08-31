namespace DocumentLifecycle.Domain.Workspaces;

public sealed class DemoWorkspace
{
    public static readonly TimeSpan InactivityLifetime = TimeSpan.FromHours(6);

    private DemoWorkspace()
    {
    }

    private DemoWorkspace(Guid id, DateTime createdAtUtc, int seedVersion)
    {
        Id = id;
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        LastActivityAtUtc = CreatedAtUtc;
        ExpiresAtUtc = CreatedAtUtc.Add(InactivityLifetime);
        SeedVersion = seedVersion;
    }

    public Guid Id { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime LastActivityAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public int SeedVersion { get; private set; }

    public int Version { get; private set; }

    public static DemoWorkspace Create(Guid id, DateTime createdAtUtc, int seedVersion)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A workspace ID is required.", nameof(id));
        }

        if (seedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(seedVersion), "Seed version must be positive.");
        }

        return new DemoWorkspace(id, createdAtUtc, seedVersion);
    }

    public bool IsExpired(DateTime utcNow) => EnsureUtc(utcNow) >= ExpiresAtUtc;

    public bool RecordMeaningfulActivity(DateTime utcNow, TimeSpan minimumWriteInterval)
    {
        utcNow = EnsureUtc(utcNow);

        if (minimumWriteInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumWriteInterval),
                "The activity write interval cannot be negative.");
        }

        if (utcNow < LastActivityAtUtc.Add(minimumWriteInterval))
        {
            return false;
        }

        LastActivityAtUtc = utcNow;
        ExpiresAtUtc = utcNow.Add(InactivityLifetime);
        Version++;
        return true;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Workspace timestamps must be UTC.", nameof(value));
        }

        return value;
    }
}
