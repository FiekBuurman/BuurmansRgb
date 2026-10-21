using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BuurmansRgb.Engine.Power;

// Like SystemPowerEvents: create this on the tray thread, which keeps pumping messages.
internal sealed partial class SystemSessionEvents : ISessionEvents, IDisposable
{
    private const int CurrentSession = -1;
    private const int SessionInfoEx = 25;

    // In WTSINFOEXW: Level, then (aligned to 8) SessionId, SessionState and SessionFlags.
    private const int LevelOffset = 0;
    private const int FlagsOffset = 16;
    private const int SessionStateLock = 0;

    public SystemSessionEvents() => SystemEvents.SessionSwitch += OnSessionSwitch;

    public event EventHandler<SessionSwitchReason>? Switched;

    // Asked from Windows each time. When that fails, the session counts as unlocked, so the effects
    // run as they would without this.
    public bool IsLocked => ReadSessionInfo() is { } info && IsLockedInfo(info);

    public void Dispose() => SystemEvents.SessionSwitch -= OnSessionSwitch;

    // The WTSINFOEXW of this session (WTSSessionInfoEx), or null when Windows does not give it.
    internal static byte[]? ReadSessionInfo()
    {
        if (!WTSQuerySessionInformation(nint.Zero, CurrentSession, SessionInfoEx, out var buffer, out var length))
        {
            Trace.TraceWarning($"Whether the session is locked could not be read (error {Marshal.GetLastPInvokeError()}).");
            return null;
        }

        try
        {
            var info = new byte[length];
            Marshal.Copy(buffer, info, 0, info.Length);
            return info;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    // Reads a WTSINFOEXW of level 1. Anything else, such as an unknown lock state, counts as unlocked.
    internal static bool IsLockedInfo(ReadOnlySpan<byte> info) =>
        info.Length >= FlagsOffset + sizeof(int)
        && BinaryPrimitives.ReadInt32LittleEndian(info[LevelOffset..]) == 1
        && BinaryPrimitives.ReadInt32LittleEndian(info[FlagsOffset..]) == SessionStateLock;

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => Switched?.Invoke(this, e.Reason);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSQuerySessionInformation(nint server, int sessionId, int infoClass, out nint buffer, out int length);

    [LibraryImport("wtsapi32.dll")]
    private static partial void WTSFreeMemory(nint memory);
}
