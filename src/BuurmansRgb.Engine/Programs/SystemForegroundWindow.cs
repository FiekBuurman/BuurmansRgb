using System.Diagnostics;
using System.Runtime.InteropServices;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Programs;

// The program of each new foreground window, from the hook the engine shares (ForegroundHook): this
// reads the exe name of its process (ProcessImageName) and leaves out the windows that do not
// count. Follow may come from any thread, so it posts to a hidden message-only window, and this
// starts and stops listening to the hook on the tray thread; while not followed it does not listen,
// and without another listener there is no hook.
internal sealed partial class SystemForegroundWindow : MessageOnlyWindow, IForegroundWindow
{
    private const int WmApp = 0x8000;
    internal const int WmFollow = WmApp + 1;
    private const int MaxClassName = 256;

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
        ProgramNames.LockScreen, "StartMenuExperienceHost.exe", "SearchHost.exe", "SearchApp.exe", "ShellExperienceHost.exe", "ShellHost.exe",
    };

    private readonly ProcessImageName _names = new();
    private readonly char[] _className = new char[MaxClassName];
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;

    // Set after the window is made; only Apply uses them, for WmFollow, which is posted and so comes
    // after the constructor. One delegate for Listen and Stop, so the hook knows it again.
    private readonly ForegroundHook _hook;
    private readonly Action<nint> _tell;
    private bool _listening;
    private volatile bool _wanted;

    // hook is shared with the rest of the engine and outlives this.
    public SystemForegroundWindow(ForegroundHook hook)
    {
        _hook = hook;
        _tell = Tell;
    }

    public event EventHandler<string?>? Changed;

    // Whether this listens to the hook now; on the tray thread.
    internal bool IsListening => _listening;

    public void Follow(bool on)
    {
        _wanted = on;
        var window = Handle;
        if (window != 0 && !PostMessage(window, WmFollow, 0, 0))
        {
            Trace.TraceWarning($"Following the foreground window could not be switched (error {Marshal.GetLastPInvokeError()}).");
        }
    }

    protected override void Release() => StopListening();

    // Whether a window of program with that class takes the user to that program; false for one
    // that passes by, and for a program without a name. A screen saver (any .scr) passes by like
    // the lock screen: it only shows while nobody touches the pc, and AutoPause pauses for it.
    internal static bool Counts(string className, string? program) =>
        program is not null
        && !PassingClasses.Contains(className)
        && !PassingPrograms.Contains(program)
        && !WindowsPrograms.IsScreenSaver(program);

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

    // As Follow asked last; several posts in a row end in the last one. When Windows does not take
    // the hook, this stops listening, so it gets no windows while not followed, and the next
    // Follow(true) tries again.
    private void Apply()
    {
        if (_wanted == _listening)
        {
            return;
        }

        if (!_wanted)
        {
            StopListening();
            return;
        }

        if (!_hook.Listen(_tell))
        {
            _hook.Stop(_tell);
            return;
        }

        _listening = true;
        Tell(ForegroundHook.Current);
    }

    private void StopListening()
    {
        if (_listening)
        {
            _hook.Stop(_tell);
            _listening = false;
        }
    }

    // Null for a window that does not count, of the engine itself or that is gone already.
    private string? ReadProgram(nint window)
    {
        var processId = ForegroundHook.ProcessOf(window);
        if (processId == 0 || processId == _ownProcessId)
        {
            return null;
        }

        var length = GetClassName(window, _className, _className.Length);
        var className = length > 0 ? new string(_className, 0, length) : string.Empty;
        return _names.TryRead(processId, out var program) && Counts(className, program) ? program : null;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetClassName(nint window, [Out] char[] className, int maxCount);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint window, int message, nint wParam, nint lParam);
}
