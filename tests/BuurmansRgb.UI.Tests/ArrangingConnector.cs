using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// Lets a test prepare the fake engine before the view model loads from it.
internal sealed class ArrangingConnector(FakeEngineConnector inner, Action<FakeEngineApi> arrange) : IEngineConnector
{
    public async Task<IEngineConnection> ConnectAsync(CancellationToken ct)
    {
        var connection = await inner.ConnectAsync(ct);
        arrange(inner.Connection.FakeApi);
        return connection;
    }
}
