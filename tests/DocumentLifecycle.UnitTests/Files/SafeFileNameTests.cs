using DocumentLifecycle.Application.Files;

namespace DocumentLifecycle.UnitTests.Files;

public sealed class SafeFileNameTests
{
    [Theory]
    [InlineData("../../private/report.pdf", "report.pdf")]
    [InlineData("..\\..\\private\\report.pdf", "report.pdf")]
    [InlineData("quarter:one?.pdf", "quarter_one_.pdf")]
    [InlineData("...", "upload")]
    public void SanitizeRemovesPathsAndUnsafeCharacters(string input, string expected)
    {
        Assert.Equal(expected, SafeFileName.Sanitize(input));
    }

    [Fact]
    public void SanitizeCapsLongNamesAndPreservesExtension()
    {
        var result = SafeFileName.Sanitize($"{new string('a', 300)}.pdf");

        Assert.Equal(255, result.Length);
        Assert.EndsWith(".pdf", result, StringComparison.Ordinal);
    }
}
