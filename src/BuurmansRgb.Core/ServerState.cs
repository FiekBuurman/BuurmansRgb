namespace BuurmansRgb.Core;

public enum ServerState
{
    // The first try is still on its way, or the connection was lost and the next try waits.
    Connecting,

    // Connected, with DeviceCount devices; that can be none.
    Connected,

    // Nothing answered at the address of the server, so most likely the server does not run.
    Unreachable,

    // Something answered at the address of the server, but connecting or loading the devices
    // failed, for example because an answer was not understood or did not come in time.
    Failed,
}
