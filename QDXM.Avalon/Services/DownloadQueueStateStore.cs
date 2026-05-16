using System.Text.Json;
using QDXM.Avalon.Core.Downloads;
using QDXM.Avalon.Core.Tools;

namespace QDXM.Avalon.Services;

public sealed class DownloadQueueStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string statePath;

    public DownloadQueueStateStore(string? statePath = null)
    {
        this.statePath = statePath ?? AppDataPaths.QueueStateFilePath;
    }

    public async Task<IReadOnlyList<DownloadQueueStateItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(statePath))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(statePath, cancellationToken).ConfigureAwait(false);
            var state = JsonSerializer.Deserialize<DownloadQueueState>(json, SerializerOptions);
            return state?.Items ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveAsync(IEnumerable<DownloadQueueStateItem> items, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        var state = new DownloadQueueState(items.ToList());
        var json = JsonSerializer.Serialize(state, SerializerOptions);
        await File.WriteAllTextAsync(statePath, json, cancellationToken).ConfigureAwait(false);
    }

    private sealed record DownloadQueueState(IReadOnlyList<DownloadQueueStateItem> Items);
}
