using System.Diagnostics;
using System.Runtime.InteropServices;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Programs;

// SetWinEventHook for EVENT_SYSTEM_FOREGROUND, out of context: Windows posts each new foreground
// window to the thread that set the hook, without a DLL in other programs and without polling, and
// this reads the exe name of its process (ProcessImageName). Like SystemPowerEvents: create this on
// the tray thread, which keeps pumping messages until the engine stops, and dispose it there.
// Follow may come from any thread, so it posts to a hidden message-only window, and the hook is set
// and taken away (UnhookWinEvent) on the tray thread; while not followed there is no hook.
internal sealed partial class SystemForegroundWindow : NativeWindow, IForegroundWindow, IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;

    // The tray menu and notifications of the engine itself do not count.
    private const uint WinEventSkipOwnProcess = 0x0002;
    private const int ObjIdWindow = 0;
    private const int WmApp = 0x8000;
    internal const int WmFollow = WmApp + 1;
    private const int MaxClassName = 256;

    // A window without a place on the screen, only for messages.
    private static readonly nint MessageOnlyParent = new(-3);

    // Windows of the shell that pass by on the way to another window, by class: the taskbars, the
    // overflow of the notification area, Alt+Tab and Win+Tab (Windows 10 and 11).
    private static readonly HashSet<string> PassingClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "TopLevelWindowForOverflowXamlIsland", "NotifyIconOverflowWindow",
        "MultitaskingViewFrame", "TaskSwitcherWnd", "XamlExplorerHostIslandWindow", "ForegroundStaging",
    };

    // Parts of Windows that show over a program without taking the user away from it: the lock
    // screen, the Start menu and search, and the panes for notifications and quick settings.
    private static readonly HashSet<string> PassingPrograms = new(ProgramNames.Comparer)
    {
        "LockApp.exe", "StartMenuExperienceHost.exe", "SearchHost.exe", "SearchApp.exe", "ShellExperienceHost.exe", "ShellHost.exe",
    };

    // Kept here, so the garbage collector leaves it while Windows may call it.
    private readonly WinEventProc _callback;
    private readonly ProcessImageName _names = new();
    private readonly char[] _className = new char[MaxClassName];
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private nint _hook;
    private volatile bool _wanted;

    public SystemForegroundWindow()
    {
        _callback = OnWinEvent;
        CreateHandle(new CreateParams { Parent = MessageOnlyParent });
    }

    public event EventHandler<string?>? Changed;

    // Whether the hook is set now; on the tray thread.
    internal bool IsHooked => _hook != 0;

    public void Follow(bool on)
    {
        _wanted = on;
        var window = Handle;
        if (window != 0 && !PostMessage(window, WmFollow, 0, 0))
        {
            Trace.TraceWarning($"Following the foreground window could not be switched (error {Marshal.GetLastPInvokeError()}).");
        }
    }

    public void Dispose()
    {
        Unhook();
        DestroyHandle();
    }

    // Whether a window of program with that class takes the user to that program; false for one
    // that passes by, and for a program without a name.
    internal static bool Counts(string className, string? program) =>
        program is not null && !PassingClasses.Contains(className) && !PassingPrograms.Contains(program);

    // A window that came to the front: its program, when it counts. Also when it is the same
    // program as before, as that may have stopped and started again in between. Called by Windows
    // and from the message loop, where a throw would end the engine or show an error dialog.
    internal void Tell(nint window)
    {
        try
        {
            if (ReadProgram(window) is { } program)
            {
                Changed?.Invoke(this, program);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmFollow)
        {
            Apply();
            return;
        }

        base.WndProc(ref m);
    }

    // As Follow asked last; several posts in a row end in the last one.
    private void Apply()
    {
        if (_wanted == IsHooked)
        {
            return;
        }

        if (!_wanted)
        {
            Unhook();
            return;
        }

        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, 0, Marshal.GetFunctionPointerForDelegate(_callback), 0, 0, WinEventOutOfContext | WinEventSkipOwnProcess);
        if (_hook == 0)
        {
            Trace.TraceWarning("The foreground window cannot be followed: SetWinEventHook failed.");
            return;
        }

        Tell(GetForegroundWindow());
    }

    private void Unhook()
    {
        if (_hook != 0 && !UnhookWinEvent(_hook))
        {
            Trace.TraceWarning("The hook on the foreground window could not be taken away.");
        }

        _hook = 0;
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

    // Null for a window that does not count, of the engine itself or that is gone already.
    private string? ReadProgram(nint window)
    {
        if (window == 0 || GetWindowThreadProcessId(window, out var processId) == 0 || processId == _ownProcessId)
        {
            return null;
        }

        var length = GetClassName(window, _className, _className.Length);
        var className = length > 0 ? new string(_className, 0, length) : string.Empty;
        return _names.TryRead(processId, out var program) && Counts(className, program) ? program : null;
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

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetClassName(nint window, [Out] char[] className, int maxCount);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint window, int message, nint wParam, nint lParam);
}
