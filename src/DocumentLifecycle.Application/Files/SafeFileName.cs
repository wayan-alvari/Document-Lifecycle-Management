namespace DocumentLifecycle.Application.Files;

public static class SafeFileName
{
    private const int MaximumLength = 255;
    private const string UnsafeCharacters = "<>:\"/\\|?*";

    public static string Sanitize(string? value)
    {
        var normalizedPath = (value ?? string.Empty).Replace('\\', '/');
        var leafName = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        var characters = leafName
            .Select(character => char.IsControl(character) || UnsafeCharacters.Contains(character)
                ? '_'
                : character)
            .ToArray();
        var sanitized = new string(characters).Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "upload";
        }

        if (sanitized.Length <= MaximumLength)
        {
            return sanitized;
        }

        var extension = Path.GetExtension(sanitized);
        var baseLength = Math.Max(1, MaximumLength - extension.Length);
        return string.Concat(sanitized.AsSpan(0, baseLength), extension);
    }
}
