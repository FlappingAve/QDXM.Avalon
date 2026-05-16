using System.Collections.ObjectModel;
using Avalonia.Threading;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Services;

public sealed class AppLogService
{
    private readonly string logFilePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public AppLogService(string? logFilePath = null)
    {
        this.logFilePath = logFilePath ?? AppDataPaths.LogFilePath;
        ClearLogFile();
    }

    public ObservableCollection<AppLogEntry> Entries { get; } = [];

    public void Info(string source, string message) => Add("Info", source, message);

    public void Warning(string source, string message) => Add("Warning", source, message);

    public void Error(string source, string message) => Add("Error", source, message);

    public void Clear()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Entries.Clear();
        }
        else
        {
            Dispatcher.UIThread.Post(Entries.Clear);
        }

        _ = ClearLogFileAsync();
    }

    private void Add(string level, string source, string message)
    {
        var entry = new AppLogEntry(DateTimeOffset.Now, level, source, message);
        if (Dispatcher.UIThread.CheckAccess())
        {
            Entries.Insert(0, entry);
        }
        else
        {
            Dispatcher.UIThread.Post(() => Entries.Insert(0, entry));
        }

        _ = AppendToLogFileAsync(entry);
    }

    private async Task AppendToLogFileAsync(AppLogEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss zzz}\t{entry.Level}\t{entry.Source}\t{Normalize(entry.Message)}{Environment.NewLine}";

            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(logFilePath, line).ConfigureAwait(false);
            }
            finally
            {
                fileLock.Release();
            }
        }
        catch
        {
            // UI logging should never crash the downloader.
        }
    }

    private async Task ClearLogFileAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await File.WriteAllTextAsync(logFilePath, string.Empty).ConfigureAwait(false);
            }
            finally
            {
                fileLock.Release();
            }
        }
        catch
        {
            // Clearing the view is still useful even if the backing file cannot be cleared.
        }
    }

    private void ClearLogFile()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            File.WriteAllText(logFilePath, string.Empty);
        }
        catch
        {
            // Logging should never block app startup.
        }
    }

    private static string Normalize(string message)
    {
        return message
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}
