namespace BuurmansRgb.Providers.OpenRgb;

internal static class ObservedTaskExtensions
{
    // A task nobody may wait for any more, such as a read the provider gave up on, can still fail
    // later, for instance once its connection is disposed. Observing its failure keeps an
    // "Unobserved task exception" out of engine.log. Gives the same task back.
    public static Task<T> Observed<T>(this Task<T> task)
    {
        _ = task.ContinueWith(
            static task => _ = task.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return task;
    }
}
