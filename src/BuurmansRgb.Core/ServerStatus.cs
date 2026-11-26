namespace BuurmansRgb.Core;

// How a provider gets on with the server its devices come from, such as the OpenRGB SDK server.
// DeviceCount is the number of devices the server gave; it only counts while Connected.
public sealed record ServerStatus(ServerState State, int DeviceCount = 0)
{
    public static ServerStatus Connecting { get; } = new(ServerState.Connecting);
}
