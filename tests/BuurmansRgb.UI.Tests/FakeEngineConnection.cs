using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.Tests;

// A connection to the fake engine; Disconnect lets it drop as when the engine goes away.
internal sealed class FakeEngineConnection : IEngineConnection
{
    public FakeEngineApi FakeApi { get; } = new();

    public IEngineApi Api => FakeApi;

    public bool IsDisposed { get; private set; }

    public event EventHandler? Disconnected;

    public void Disconnect() => Disconnected?.Invoke(this, EventArgs.Empty);

    public void Dispose() => IsDisposed = true;
}
