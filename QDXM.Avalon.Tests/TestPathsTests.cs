namespace QDXM.Avalon.Tests;

public sealed class TestPathsTests
{
    [Fact]
    public void TestingRoot_IsRepositoryTestingDirectory()
    {
        var expectedRoot = FindRepositoryTestingRoot();

        Assert.Equal(
            Path.GetFullPath(expectedRoot),
            Path.GetFullPath(TestPaths.TestingRoot));
    }

    private static string FindRepositoryTestingRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QDXM.Avalon.sln")))
            {
                return Path.Combine(current.FullName, "Testing");
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find QDXM.Avalon.sln above test output directory '{AppContext.BaseDirectory}'.");
    }
}
