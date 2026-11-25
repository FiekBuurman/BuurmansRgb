using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// Writes down every status the provider tells, in order, so a test can check the changes after
// the fact; the provider tells them from its own thread, so the list is read under a lock.
internal sealed class ServerStatusRecorder
{
    private readonly List<ServerStatus> _statuses = [];

    public ServerStatusRecorder(OpenRgbDeviceProvider provider) =>
        provider.StatusChanged += (_, status) =>
        {
            lock (_statuses)
            {
                _statuses.Add(status);
            }
        };

    public ServerStatus[] Snapshot()
    {
        lock (_statuses)
        {
            return [.. _statuses];
        }
    }
}
