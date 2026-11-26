using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// A restart of the Windows services that a test lets succeed, fail or hang.
internal sealed class FakeServicesRestart : IServicesRestart
{
    public ServicesRestartReport Report { get; set; } = new(ServiceOutcome.Restarted, ServiceOutcome.Restarted);

    public Exception? Exception { get; set; }

    public Action? OnRestart { get; set; }

    // Set to keep the restart going until the test completes it.
    public TaskCompletionSource<ServicesRestartReport>? Pending { get; set; }

    public int Count { get; private set; }

    public Task<ServicesRestartReport> RestartAsync(CancellationToken ct)
    {
        Count++;
        OnRestart?.Invoke();
        if (Pending is not null)
        {
            return Pending.Task;
        }

        return Exception is null ? Task.FromResult(Report) : Task.FromException<ServicesRestartReport>(Exception);
    }
}
