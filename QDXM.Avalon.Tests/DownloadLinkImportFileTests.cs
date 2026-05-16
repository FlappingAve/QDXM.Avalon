using QDXM.Avalon.Core.Downloads;

namespace QDXM.Avalon.Tests;

public sealed class DownloadLinkImportFileTests
{
    [Theory]
    [InlineData("--import", @"C:\Links\qobuz.txt")]
    [InlineData("/import", @"C:\Links\qobuz.txt")]
    public void TryGetImportFilePath_ReadsFollowingArgument(string flag, string path)
    {
        var parsed = DownloadLinkImportFile.TryGetImportFilePath([flag, path], out var filePath, out var errorMessage);

        Assert.True(parsed);
        Assert.Equal(path, filePath);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryGetImportFilePath_ReadsEqualsArgument()
    {
        var parsed = DownloadLinkImportFile.TryGetImportFilePath(
            [@"--import=C:\Links\qobuz.txt"],
            out var filePath,
            out var errorMessage);

        Assert.True(parsed);
        Assert.Equal(@"C:\Links\qobuz.txt", filePath);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryGetImportFilePath_RejectsMissingPath()
    {
        var parsed = DownloadLinkImportFile.TryGetImportFilePath(["--import"], out var filePath, out var errorMessage);

        Assert.False(parsed);
        Assert.Null(filePath);
        Assert.Contains("missing", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetImportFilePath_RejectsEmptyEqualsArgument()
    {
        var parsed = DownloadLinkImportFile.TryGetImportFilePath(["--import="], out var filePath, out var errorMessage);

        Assert.False(parsed);
        Assert.Equal(string.Empty, filePath);
        Assert.Contains("missing", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetImportFilePath_ReturnsNoErrorWhenImportWasNotRequested()
    {
        var parsed = DownloadLinkImportFile.TryGetImportFilePath(["qdxma://album/1"], out var filePath, out var errorMessage);

        Assert.False(parsed);
        Assert.Null(filePath);
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task TryReadLinks_ReturnsTrimmedNonBlankLines()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var importPath = workspace.FilePath("links.txt");
        await File.WriteAllTextAsync(importPath, """
            https://open.qobuz.com/album/1

              qdxma://track/2

            https://open.qobuz.com/playlist/3
            """);

        var read = DownloadLinkImportFile.TryReadLinks(importPath, out var links, out var errorMessage);

        Assert.True(read);
        Assert.Null(errorMessage);
        Assert.Equal(
            [
                "https://open.qobuz.com/album/1",
                "qdxma://track/2",
                "https://open.qobuz.com/playlist/3"
            ],
            links);
    }

    [Fact]
    public void TryReadLinks_ReturnsFailureWhenFileDoesNotExist()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var importPath = workspace.FilePath("missing.txt");

        var read = DownloadLinkImportFile.TryReadLinks(importPath, out var links, out var errorMessage);

        Assert.False(read);
        Assert.Empty(links);
        Assert.Contains("not found", errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(importPath, errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryReadLinks_ReturnsFailureWhenPathIsBlank()
    {
        var read = DownloadLinkImportFile.TryReadLinks(" ", out var links, out var errorMessage);

        Assert.False(read);
        Assert.Empty(links);
        Assert.Contains("empty", errorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
