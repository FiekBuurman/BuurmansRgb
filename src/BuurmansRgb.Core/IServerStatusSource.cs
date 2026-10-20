namespace BuurmansRgb.Core;

// Tells how a provider gets on with its server. StatusChanged is raised only when the status
// changes, one change after the other and on any thread; it carries the new status.
public interface IServerStatusSource
{
    ServerStatus Status { get; }

    event EventHandler<ServerStatus>? StatusChanged;
}
