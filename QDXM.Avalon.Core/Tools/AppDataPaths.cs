namespace QDXM.Avalon.Core.Tools;

public static class AppDataPaths
{
    public const string AppName = "QDXM Avalon";
    public const string CredentialTargetName = "QDXM Avalon/UserAuth";
    public const string PreviewCredentialTargetName = "QDXM Avalon/PreviewUserAuth";

    public static string AppDirectory => GetAppDirectory();

    public static string DataDirectory => Path.Combine(AppDirectory, "Avalon-Data");

    public static string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    public static string QueueStateFilePath => Path.Combine(DataDirectory, "queue-state.json");

    public static string ProtocolQueueDirectory => Path.Combine(DataDirectory, "protocol-queue");

    public static string LogFilePath => Path.Combine(DataDirectory, "logs", "app.log");

    public static string CoverCacheDirectory => Path.Combine(DataDirectory, "covers");

    public static string QueueCoverCacheDirectory => Path.Combine(DataDirectory, "queue-covers");

    public static string SearchImageCacheDirectory => Path.Combine(DataDirectory, "search-images");

    private static string GetAppDirectory()
    {
        var processPath = Environment.ProcessPath;
        var directory = string.IsNullOrWhiteSpace(processPath)
            ? null
            : Path.GetDirectoryName(processPath);

        return string.IsNullOrWhiteSpace(directory)
            ? AppContext.BaseDirectory
            : directory;
    }
}
