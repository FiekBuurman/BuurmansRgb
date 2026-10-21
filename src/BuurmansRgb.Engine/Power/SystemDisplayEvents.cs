using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BuurmansRgb.Engine.Power;

// RegisterPowerSettingNotification for GUID_CONSOLE_DISPLAY_STATE on a hidden message-only window:
// the screen of the console goes off, is dimmed or goes on again. Windows sends the state once
// right after registering, through the message queue, so IsOff knows it only once the tray thread
// pumps. Like SystemPowerEvents: create this on the tray thread, which keeps pumping messages
// until the engine stops, and dispose it there.
internal sealed partial class SystemDisplayEvents : NativeWindow, IDisplayEvents, IDisposable
{
    // GUID_CONSOLE_DISPLAY_STATE.
    internal static readonly Guid ConsoleDisplayState = new("6fe69556-704a-47a0-8f24-c28d936fda47");

    private const int WmPowerBroadcast = 0x0218;
    private const int PbtPowerSettingChange = 0x8013;
    private const int DeviceNotifyWindowHandle = 0;

    // In POWERBROADCAST_SETTING: the GUID of the setting, DataLength, and then the data: for this
    // setting a DWORD that is 0 (off), 1 (on) or 2 (dimmed).
    private const int DataLengthOffset = 16;
    private const int DataOffset = 20;
    private const int DisplayStateLength = DataOffset + sizeof(int);
    private const int DisplayOff = 0;

    // A window without a place on the screen, only for messages.
    private static readonly nint MessageOnlyParent = new(-3);

    private nint _registration;
    private volatile bool _isOff;

    // When Windows does not take the registration, the screen counts as on, so the effects run as
    // they would without this.
    public SystemDisplayEvents()
    {
        CreateHandle(new CreateParams { Parent = MessageOnlyParent });
        var setting = ConsoleDisplayState;
        _registration = RegisterPowerSettingNotification(Handle, in setting, DeviceNotifyWindowHandle);
        if (_registration == 0)
        {
            Trace.TraceWarning($"Whether the screen is off cannot be followed (error {Marshal.GetLastPInvokeError()}).");
        }
    }

    public event EventHandler<bool>? Switched;

    public bool IsOff => _isOff;

    public void Dispose()
    {
        if (_registration != 0 && !UnregisterPowerSettingNotification(_registration))
        {
            Trace.TraceWarning($"The screen state could not be unregistered (error {Marshal.GetLastPInvokeError()}).");
        }

        _registration = 0;
        DestroyHandle();
    }

    // Reads a POWERBROADCAST_SETTING: whether the screen is off, false when it is on or dimmed, and
    // null when it is about another setting or too short.
    internal static bool? ReadIsOff(ReadOnlySpan<byte> setting)
    {
        if (setting.Length < DisplayStateLength
            || new Guid(setting[..DataLengthOffset]) != ConsoleDisplayState
            || BinaryPrimitives.ReadInt32LittleEndian(setting[DataLengthOffset..]) < sizeof(int))
        {
            return null;
        }

        return BinaryPrimitives.ReadInt32LittleEndian(setting[DataOffset..]) == DisplayOff;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmPowerBroadcast && m.WParam == PbtPowerSettingChange && m.LParam != 0)
        {
            // The data only when it is there, so nothing is read past the end of the setting.
            var dataLength = Marshal.ReadInt32(m.LParam, DataLengthOffset);
            var setting = new byte[dataLength >= sizeof(int) ? DisplayStateLength : DataOffset];
            Marshal.Copy(m.LParam, setting, 0, setting.Length);
            if (ReadIsOff(setting) is { } off && off != _isOff)
            {
                _isOff = off;
                Switched?.Invoke(this, off);
            }

            m.Result = 1;
            return;
        }

        base.WndProc(ref m);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint RegisterPowerSettingNotification(nint recipient, in Guid powerSettingGuid, int flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterPowerSettingNotification(nint handle);
}
