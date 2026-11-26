using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// Connects the app to a new FakeEngineConnection each time, or fails with ConnectException.
internal sealed class FakeEngineConnector : IEngineConnector
{
    public Exception? ConnectException { get; set; }

    public FakeEngineConnection Connection { get; private set; } = new();

    public int ConnectCount { get; private set; }

    public Task<IEngineConnection> ConnectAsync(CancellationToken ct)
    {
        ConnectCount++;
        if (ConnectException is not null)
        {
            return Task.FromException<IEngineConnection>(ConnectException);
        }

        Connection = new FakeEngineConnection();
        return Task.FromResult<IEngineConnection>(Connection);
    }
}
