using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BuurmansRgb.Engine.Hotkeys;

// RegisterHotKey on a hidden message-only window. Like SystemPowerEvents: create this on the tray
// thread, which keeps pumping messages until the engine stops, and dispose it there.
internal sealed partial class WindowsGlobalHotkeys : MessageOnlyWindow, IGlobalHotkeys
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;

    // Held down, the key does its work once, not again with every repeat of the keyboard.
    private const uint ModNoRepeat = 0x4000;

    private readonly Dictionary<Keys, (int Id, Action Pressed)> _registered = [];

    public bool Register(Keys key, Action pressed)
    {
        Unregister(key);
        var id = ToId(key);
        if (!RegisterHotKey(Handle, id, ToModifiers(key), (uint)(key & Keys.KeyCode)))
        {
            Trace.TraceWarning($"The hotkey {key} could not be registered (error {Marshal.GetLastPInvokeError()}); another app may have it.");
            return false;
        }

        _registered[key] = (id, pressed);
        return true;
    }

    public void Unregister(Keys key)
    {
        if (_registered.Remove(key, out var hotkey) && !UnregisterHotKey(Handle, hotkey.Id))
        {
            Trace.TraceWarning($"The hotkey {key} could not be unregistered (error {Marshal.GetLastPInvokeError()}).");
        }
    }

    // Gives the keys back before the window goes, so another app can have them again.
    protected override void Release()
    {
        foreach (var key in _registered.Keys.ToList())
        {
            Unregister(key);
        }
    }

    internal static uint ToModifiers(Keys key) =>
        ModNoRepeat
        | (key.HasFlag(Keys.Alt) ? ModAlt : 0)
        | (key.HasFlag(Keys.Control) ? ModControl : 0)
        | (key.HasFlag(Keys.Shift) ? ModShift : 0);

    // The same id for a key every time, made of its modifiers and its key code: at most 0x7FF, so
    // always in the 0x0000-0xBFFF that Windows leaves to an app, however often it is registered.
    internal static int ToId(Keys key) =>
        (int)((((uint)key >> 16) & 0x7) << 8 | ((uint)key & 0xFF));

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey)
        {
            var id = (int)m.WParam;
            foreach (var (_, hotkey) in _registered)
            {
                if (hotkey.Id == id)
                {
                    hotkey.Pressed();
                    break;
                }
            }

            return;
        }

        base.WndProc(ref m);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);
}
