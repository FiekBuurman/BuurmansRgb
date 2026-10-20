using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

internal interface ISessionEvents
{
    // Raised when the Windows session of the engine is locked, unlocked, connected or
    // disconnected, on any thread.
    event EventHandler<SessionSwitchReason>? Switched;
}
