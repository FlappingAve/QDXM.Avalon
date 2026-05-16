namespace QDXM.Avalon.Core.Tools;

public static class DirectoryContentsCleaner
{
    public static void Clear(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
        }
        catch
        {
            return;
        }

        foreach (var entryPath in Directory.EnumerateFileSystemEntries(directoryPath).ToArray())
        {
            DeleteEntry(entryPath);
        }
    }

    private static void DeleteEntry(string entryPath)
    {
        try
        {
            if (Directory.Exists(entryPath))
            {
                Directory.Delete(entryPath, recursive: true);
                return;
            }

            File.Delete(entryPath);
        }
        catch
        {
        }
    }
}
