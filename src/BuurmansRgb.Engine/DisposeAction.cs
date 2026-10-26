namespace BuurmansRgb.Engine;

// Runs an action when it is disposed, so a step on the way out keeps its place among the usings.
internal sealed class DisposeAction(Action action) : IDisposable
{
    private Action? _action = action;

    public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
}
