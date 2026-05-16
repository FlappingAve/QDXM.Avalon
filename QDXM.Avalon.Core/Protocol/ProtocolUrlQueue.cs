using System.Diagnostics;

namespace QDXM.Avalon.Core.Protocol;

public sealed class ProtocolUrlQueue : IDisposable
{
    private const int ReadFailureRetryDelayMilliseconds = 500;
    private const int MaxReadFailuresPerFile = 3;
    private const string QueueFileExtension = ".url";
    private const string WarningFileExtension = ".warning";
    private const string TemporaryFileExtension = ".tmp";
    private readonly string queueDirectory;
    private readonly Func<string, string> readAllText;
    private readonly object lockObject = new();
    private readonly Dictionary<string, int> readFailureCounts = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? watcher;
    private Timer? debounceTimer;
    private bool isProcessing;
    private bool processAgain;

    public ProtocolUrlQueue(string queueDirectory)
        : this(queueDirectory, File.ReadAllText)
    {
    }

    internal ProtocolUrlQueue(string queueDirectory, Func<string, string> readAllText)
    {
        this.queueDirectory = queueDirectory;
        this.readAllText = readAllText;
    }

    public event Action<string>? UrlReceived;

    public event Action<string>? WarningReceived;

    private string PendingDirectory => Path.Combine(queueDirectory, "pending");
    private string InProgressDirectory => Path.Combine(queueDirectory, "in-progress");
    private string FailedDirectory => Path.Combine(queueDirectory, "failed");

    public void Initialize()
    {
        try
        {
            EnsureQueueDirectories();
            RecoverInProgressFiles();

            watcher = new FileSystemWatcher(PendingDirectory)
            {
                Filter = "*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            watcher.Created += OnQueueFileChanged;
            watcher.Changed += OnQueueFileChanged;
            watcher.Renamed += OnQueueFileChanged;
            watcher.EnableRaisingEvents = true;
            ProcessQueue();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize protocol queue: {ex.Message}");
        }
    }

    public void AddToQueue(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        EnsureQueueDirectories();

        WriteQueueFile(url, QueueFileExtension);
    }

    public void AddWarningToQueue(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        EnsureQueueDirectories();

        WriteQueueFile(message, WarningFileExtension);
    }

    public void ProcessQueue()
    {
        lock (lockObject)
        {
            if (isProcessing)
            {
                processAgain = true;
                return;
            }

            isProcessing = true;
        }

        try
        {
            EnsureQueueDirectories();
            while (true)
            {
                var completedDrain = DrainPendingFiles();

                lock (lockObject)
                {
                    if (!completedDrain)
                    {
                        isProcessing = false;
                        processAgain = false;
                        ScheduleProcessQueueCore(ReadFailureRetryDelayMilliseconds);
                        return;
                    }

                    if (!processAgain && !HasPendingQueueFiles())
                    {
                        isProcessing = false;
                        return;
                    }

                    processAgain = false;
                }
            }
        }
        catch
        {
            lock (lockObject)
            {
                isProcessing = false;
                processAgain = false;
            }

            throw;
        }
    }

    private bool DrainPendingFiles()
    {
        foreach (var pendingPath in GetPendingQueueFiles())
        {
            var claimedPath = ClaimPendingFile(pendingPath);
            if (claimedPath is null)
            {
                continue;
            }

            if (!TryReadQueuedUrl(claimedPath, out var url))
            {
                return HandleReadFailure(claimedPath);
            }

            ClearReadFailureCount(claimedPath);
            if (string.IsNullOrWhiteSpace(url))
            {
                DeleteQueueFile(claimedPath);
                continue;
            }

            if (IsWarningQueueFile(claimedPath))
            {
                WarningReceived?.Invoke(url);
            }
            else
            {
                UrlReceived?.Invoke(url);
            }

            DeleteQueueFile(claimedPath);
        }

        return true;
    }

    private bool HasPendingQueueFiles()
    {
        return Directory.EnumerateFiles(PendingDirectory).Any(IsSupportedQueueFile);
    }

    private void RecoverInProgressFiles()
    {
        foreach (var inProgressPath in Directory
            .EnumerateFiles(InProgressDirectory)
            .Where(IsSupportedQueueFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray())
        {
            var pendingPath = GetAvailablePath(PendingDirectory, Path.GetFileName(inProgressPath));
            RetryFileAction(() => File.Move(inProgressPath, pendingPath));
        }
    }

    private string? ClaimPendingFile(string pendingPath)
    {
        if (!File.Exists(pendingPath))
        {
            return null;
        }

        var inProgressPath = GetAvailablePath(InProgressDirectory, Path.GetFileName(pendingPath));
        try
        {
            RetryFileAction(() => File.Move(pendingPath, inProgressPath));
            return inProgressPath;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private bool TryReadQueuedUrl(string claimedPath, out string? url)
    {
        try
        {
            url = readAllText(claimedPath).Trim();
            return true;
        }
        catch (IOException)
        {
            url = null;
            return false;
        }
    }

    private bool HandleReadFailure(string claimedPath)
    {
        var failures = IncrementReadFailureCount(claimedPath);
        if (failures >= MaxReadFailuresPerFile)
        {
            var failedMove = MoveClaimedFileToFailed(claimedPath);
            var warning = failedMove.Moved
                ? $"Protocol queue file could not be read after {failures} attempts and was moved to failed: {failedMove.Path}"
                : $"Protocol queue file could not be read after {failures} attempts and could not be moved to failed: {failedMove.Path}";
            WarningReceived?.Invoke(
                warning);
            ClearReadFailureCount(claimedPath);
            return true;
        }

        ReturnClaimedFile(claimedPath);
        return false;
    }

    private void ReturnClaimedFile(string claimedPath)
    {
        try
        {
            var pendingPath = GetAvailablePath(PendingDirectory, Path.GetFileName(claimedPath));
            RetryFileAction(() => File.Move(claimedPath, pendingPath));
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to return protocol queue file to pending: {ex.Message}");
        }
    }

    private FailedMoveResult MoveClaimedFileToFailed(string claimedPath)
    {
        try
        {
            var failedPath = GetAvailablePath(FailedDirectory, Path.GetFileName(claimedPath));
            RetryFileAction(() => File.Move(claimedPath, failedPath));
            return new FailedMoveResult(true, failedPath);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Failed to move protocol queue file to failed: {ex.Message}");
            return new FailedMoveResult(false, claimedPath);
        }
    }

    private sealed record FailedMoveResult(bool Moved, string Path);

    private int IncrementReadFailureCount(string path)
    {
        var fileName = Path.GetFileName(path);
        var failures = readFailureCounts.TryGetValue(fileName, out var existingFailures)
            ? existingFailures + 1
            : 1;
        readFailureCounts[fileName] = failures;
        return failures;
    }

    private void ClearReadFailureCount(string path)
    {
        readFailureCounts.Remove(Path.GetFileName(path));
    }

    private void EnsureQueueDirectories()
    {
        Directory.CreateDirectory(queueDirectory);
        Directory.CreateDirectory(PendingDirectory);
        Directory.CreateDirectory(InProgressDirectory);
        Directory.CreateDirectory(FailedDirectory);
    }

    private static string GetAvailablePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return Path.Combine(directory, $"{nameWithoutExtension}-{Guid.NewGuid():N}{extension}");
    }

    private void WriteQueueFile(string text, string extension)
    {
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}";
        var temporaryPath = Path.Combine(PendingDirectory, fileName + TemporaryFileExtension);
        var pendingPath = Path.Combine(PendingDirectory, fileName + extension);

        RetryFileAction(() => File.WriteAllText(temporaryPath, text.Trim()));
        RetryFileAction(() => File.Move(temporaryPath, pendingPath));
    }

    private IEnumerable<string> GetPendingQueueFiles()
    {
        return Directory
            .EnumerateFiles(PendingDirectory)
            .Where(IsSupportedQueueFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsSupportedQueueFile(string path)
    {
        return string.Equals(Path.GetExtension(path), QueueFileExtension, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetExtension(path), WarningFileExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWarningQueueFile(string path)
    {
        return string.Equals(Path.GetExtension(path), WarningFileExtension, StringComparison.OrdinalIgnoreCase);
    }

    private void OnQueueFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedQueueFile(e.FullPath))
        {
            return;
        }

        ScheduleProcessQueue(200);
    }

    private void ScheduleProcessQueue(int dueTimeMilliseconds)
    {
        lock (lockObject)
        {
            ScheduleProcessQueueCore(dueTimeMilliseconds);
        }
    }

    private void ScheduleProcessQueueCore(int dueTimeMilliseconds)
    {
        debounceTimer?.Dispose();
        debounceTimer = new Timer(_ => ProcessQueue(), null, dueTimeMilliseconds, Timeout.Infinite);
    }

    private static void DeleteQueueFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static void RetryFileAction(Action action)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
        }
    }

    public void Dispose()
    {
        debounceTimer?.Dispose();
        watcher?.Dispose();
    }
}
