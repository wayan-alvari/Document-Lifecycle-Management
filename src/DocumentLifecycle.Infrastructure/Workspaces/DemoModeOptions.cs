namespace DocumentLifecycle.Infrastructure.Workspaces;

public sealed class DemoModeOptions
{
    public const string SectionName = "DemoMode";

    public bool Enabled { get; init; }

    public int SeedVersion { get; init; } = 1;

    public int ActivityWriteIntervalMinutes { get; init; } = 5;

    public int CleanupIntervalMinutes { get; init; } = 30;

    public int CookieLifetimeDays { get; init; } = 30;

    public TimeSpan ActivityWriteInterval => TimeSpan.FromMinutes(ActivityWriteIntervalMinutes);

    public TimeSpan CleanupInterval => TimeSpan.FromMinutes(CleanupIntervalMinutes);
}
