namespace QDXM.Avalon.Services;

public interface ISearchMemoryCleanupScheduler
{
    void Schedule(TimeSpan delay, Func<bool> isCurrent, Action cleanup);
}

public sealed class BackgroundSearchMemoryCleanupScheduler : ISearchMemoryCleanupScheduler
{
    public void Schedule(TimeSpan delay, Func<bool> isCurrent, Action cleanup)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay).ConfigureAwait(false);

            if (isCurrent())
            {
                cleanup();
            }
        });
    }
}
