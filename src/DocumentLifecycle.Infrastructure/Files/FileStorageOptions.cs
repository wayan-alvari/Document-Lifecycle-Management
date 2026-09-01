namespace DocumentLifecycle.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; init; } = "../uploads";

    public long MaximumFileSizeBytes { get; init; } = 10 * 1024 * 1024;
}
