using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Tests;

public sealed class DirectoryContentsCleanerTests
{
    [Fact]
    public void Clear_CreatesMissingDirectory()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var directoryPath = Path.Combine(workspace.DirectoryPath, "search-images");

        DirectoryContentsCleaner.Clear(directoryPath);

        Assert.True(Directory.Exists(directoryPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(directoryPath));
    }

    [Fact]
    public void Clear_RemovesOnlyDirectoryContents()
    {
        using var workspace = TestPaths.CreateWorkspace();
        var directoryPath = workspace.CreateDirectory("search-images");
        var childDirectory = Path.Combine(directoryPath, "old-session");
        var siblingDirectory = workspace.CreateDirectory("sibling");
        var staleImagePath = Path.Combine(directoryPath, "stale.img");
        var nestedImagePath = Path.Combine(childDirectory, "nested.img");
        var siblingFilePath = Path.Combine(siblingDirectory, "keep.txt");

        Directory.CreateDirectory(childDirectory);
        File.WriteAllText(staleImagePath, "stale");
        File.WriteAllText(nestedImagePath, "nested");
        File.WriteAllText(siblingFilePath, "keep");

        DirectoryContentsCleaner.Clear(directoryPath);

        Assert.True(Directory.Exists(directoryPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(directoryPath));
        Assert.True(Directory.Exists(siblingDirectory));
        Assert.True(File.Exists(siblingFilePath));
    }
}
