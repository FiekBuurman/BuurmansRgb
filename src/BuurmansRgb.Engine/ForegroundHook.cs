using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BuurmansRgb.Engine;

// SetWinEventHook for EVENT_SYSTEM_FOREGROUND, out of context, once for all of the engine that
// follows the foreground window (SystemForegroundWindow, SystemScreenSaverEvents): Windows posts each
// new foreground window to the thread that set the hook, without a DLL in other programs and
// without polling, and that once however many listen. The hook is there only while somebody listens,
// and is taken away (UnhookWinEvent) after the last. Only on the tray thread, which keeps pumping
// messages until the engine stops: Listen, Stop and the calls to the listeners all happen there, so
// nothing needs a lock.
internal sealed partial class ForegroundHook : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;

    // The tray menu and notifications of the engine itself do not count for anybody.
    private const uint WinEventSkipOwnProcess = 0x0002;
    private const int ObjIdWindow = 0;

    // Kept here, so the garbage collector leaves it while Windows may call it.
    private readonly WinEventProc _callback;

    // Replaced, never changed, so a listener that stops while it is called does not upset the loop.
    private Action<nint>[] _listeners = [];
    private nint _hook;

    public ForegroundHook() => _callback = OnWinEvent;

    // The window in the foreground now; 0 when there is none, such as while it changes.
    public static nint Current => GetForegroundWindow();

    // Whether the hook is set now.
    internal bool IsSet => _hook != 0;

    // The process of window; 0 when it is gone.
    public static uint ProcessOf(nint window) =>
        window != 0 && GetWindowThreadProcessId(window, out var processId) != 0 ? processId : 0;

    // From now on listener gets each window that comes to the front, also another window of the
    // program in front, but not one of the engine itself. False when Windows does not take the hook:
    // then listener gets nothing, and may ask again later.
    public bool Listen(Action<nint> listener)
    {
        if (_hook == 0)
        {
            _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, 0, Marshal.GetFunctionPointerForDelegate(_callback), 0, 0, WinEventOutOfContext | WinEventSkipOwnProcess);
            if (_hook == 0)
            {
                Trace.TraceWarning("The foreground window cannot be followed: SetWinEventHook failed.");
                return false;
            }
        }

        if (!_listeners.Contains(listener))
        {
            _listeners = [.. _listeners, listener];
        }

        return true;
    }

    // After the last listener, the hook goes.
    public void Stop(Action<nint> listener)
    {
        _listeners = [.. _listeners.Where(other => other != listener)];
        if (_listeners.Length == 0)
        {
            Unhook();
        }
    }

    public void Dispose()
    {
        _listeners = [];
        Unhook();
    }

    private void Unhook()
    {
        if (_hook != 0 && !UnhookWinEvent(_hook))
        {
            Trace.TraceWarning("The hook on the foreground window could not be taken away.");
        }

        _hook = 0;
    }

    // Gives window to each listener. Called by Windows from the message loop, where a throw would end
    // the engine; a listener that fails does not keep the window from the others.
    internal void Tell(nint window)
    {
        foreach (var listener in _listeners)
        {
            try
            {
                listener(window);
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
        }
    }

    private void OnWinEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint threadId, uint time)
    {
        // An event may still be in the queue after the hook went.
        if (hook != _hook || _hook == 0 || eventType != EventSystemForeground || objectId != ObjIdWindow || window == 0)
        {
            return;
        }

        Tell(window);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventProc(nint hook, uint eventType, nint window, int objectId, int childId, uint threadId, uint time);

    [LibraryImport("user32.dll")]
    private static partial nint SetWinEventHook(uint eventMin, uint eventMax, nint module, nint callback, uint processId, uint threadId, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hook);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);
}
