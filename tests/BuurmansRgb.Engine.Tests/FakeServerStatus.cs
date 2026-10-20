using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Tests;

// Stands in for the status of the OpenRGB provider; the test sets it.
internal sealed class FakeServerStatus : IServerStatusSource
{
    public event EventHandler<ServerStatus>? StatusChanged;

    public ServerStatus Status { get; private set; } = ServerStatus.Connecting;

    public bool HasListeners => StatusChanged is not null;

    public void Change(ServerStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }
}
