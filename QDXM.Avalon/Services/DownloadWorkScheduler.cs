namespace QDXM.Avalon.Services;

public interface IDownloadWorkScheduler
{
    Task RunAsync(Func<Task> work);
}

public sealed class ThreadPoolDownloadWorkScheduler : IDownloadWorkScheduler
{
    public Task RunAsync(Func<Task> work)
    {
        return Task.Run(work);
    }
}
