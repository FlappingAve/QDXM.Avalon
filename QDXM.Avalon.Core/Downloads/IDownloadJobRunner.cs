namespace QDXM.Avalon.Core.Downloads;

public interface IDownloadJobRunner
{
    IAsyncEnumerable<DownloadEvent> RunAsync(
        DownloadQueueItem item,
        CancellationToken cancellationToken);
}
