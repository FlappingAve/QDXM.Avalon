namespace QDXM.Avalon.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        DirectoryPath = Path.Combine(TestPaths.TestingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public string FilePath(string fileName)
    {
        return Path.Combine(DirectoryPath, fileName);
    }

    public string CreateDirectory(string name)
    {
        var path = Path.Combine(DirectoryPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                }
            }
            catch
            {
            }

            Thread.Sleep(50);
        }

        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
        catch
        {
            // A failed cleanup should not hide the test failure that caused it.
        }
    }
}

internal static class TestPaths
{
    public static string TestingRoot { get; } = FindTestingRoot();
    public static string DownloadRoot { get; } = Path.Combine(TestingRoot, "Sort");

    public static TestWorkspace CreateWorkspace()
    {
        return new TestWorkspace();
    }

    private static string FindTestingRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QDXM.Avalon.sln")))
            {
                var testingRoot = Path.Combine(current.FullName, "Testing");
                Directory.CreateDirectory(testingRoot);
                return testingRoot;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find QDXM.Avalon.sln above test output directory '{AppContext.BaseDirectory}'.");
    }
}
