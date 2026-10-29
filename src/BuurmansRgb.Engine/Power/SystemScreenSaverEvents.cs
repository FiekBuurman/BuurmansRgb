using System.Diagnostics;
using System.Runtime.InteropServices;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Power;

// Whether the screen saver runs, from the foreground window (ForegroundHook), without polling: a
// screen saver is a program with the extension .scr, and its window comes to the front when Windows
// starts it. SystemParametersInfo(SPI_GETSCREENSAVERRUNNING) confirms it, as Windows sets that only
// for a screen saver it starts itself: a .scr in front for "Voorbeeld" in the screen saver settings
// does not count. Asked only when a .scr comes to the front, so any other window costs its exe name.
// Should Windows set it only a little after the window came to the front, it is asked once more a
// second later, while that window is still in front; a timer that runs once, not polling. Whatever
// window comes to the front after it ends it, without asking Windows, which may still say it runs
// while the screen saver closes. All but the lock screen: when the screen saver asks to sign in
// again, LockApp comes to the front before Windows reports the lock, and the lights must not go
// bright in between; unlocking ends it anyway (AutoPause). Create and dispose it on the tray
// thread, like ForegroundHook; the second ask comes on the thread of the timer, so the lock keeps
// the changes in order.
internal sealed partial class SystemScreenSaverEvents : IScreenSaverEvents, IDisposable
{
    internal static readonly TimeSpan AskAgainAfter = TimeSpan.FromSeconds(1);

    private const uint SpiGetScreenSaverRunning = 0x0072;

    private readonly ForegroundHook _hook;
    private readonly Action<nint> _tell;
    private readonly ProcessImageName _names = new();
    private readonly Func<nint, string?> _programOf;
    private readonly Func<bool> _windowsSaysRunning;
    private readonly Func<nint> _foreground;
    private readonly ITimer _askAgain;
    private readonly Lock _gate = new();

    // The window of a .scr that Windows did not confirm yet; 0 when there is nothing to ask again.
    private nint _askAbout;
    private bool _disposed;
    private volatile bool _isRunning;

    // While Windows does not take the hook, the screen saver counts as not running, so the effects
    // run as they would without this. hook is shared with the rest of the engine and outlives this.
    public SystemScreenSaverEvents(ForegroundHook hook)
        : this(hook, programOf: null, WindowsSaysRunning, static () => ForegroundHook.Current, TimeProvider.System)
    {
    }

    // programOf gives the exe name of a window, null when it is gone or cannot be read (by default
    // through ProcessImageName, on the tray thread only); windowsSaysRunning asks
    // SPI_GETSCREENSAVERRUNNING and foreground gives the window in front now.
    internal SystemScreenSaverEvents(ForegroundHook hook, Func<nint, string?>? programOf, Func<bool> windowsSaysRunning, Func<nint> foreground, TimeProvider time)
    {
        _hook = hook;
        _tell = Tell;
        _programOf = programOf ?? ReadProgram;
        _windowsSaysRunning = windowsSaysRunning;
        _foreground = foreground;
        _askAgain = time.CreateTimer(_ => AskAgain(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        // Still listening: should a scene that follows the foreground get the hook later, this gets
        // the windows from then on too, but not the one in front at that moment; a screen saver
        // that runs then does not count, only the next one.
        if (!_hook.Listen(_tell))
        {
            Trace.TraceWarning("Whether the screen saver runs cannot be followed until the foreground hook is set.");
            return;
        }

        // An engine that starts while the screen saver runs; Switched has no listeners yet.
        Tell(_foreground());
    }

    public event EventHandler<bool>? Switched;

    public bool IsRunning => _isRunning;

    public void Dispose()
    {
        _hook.Stop(_tell);
        lock (_gate)
        {
            _disposed = true;
            _askAbout = 0;
            _askAgain.Dispose();
        }
    }

    // A window that came to the front; the lock screen changes nothing. A newer window than the .scr
    // to ask about makes that ask moot.
    internal void Tell(nint window)
    {
        lock (_gate)
        {
            _askAbout = 0;
            var program = _programOf(window);
            if (WindowsPrograms.IsLockScreen(program))
            {
                return;
            }

            if (!WindowsPrograms.IsScreenSaver(program))
            {
                ShowLocked(false);
                return;
            }

            if (_windowsSaysRunning())
            {
                ShowLocked(true);
                return;
            }

            // "Voorbeeld" in the screen saver settings, or a Windows that sets the flag a bit later.
            Trace.TraceInformation($"{program} came to the front, but Windows says no screen saver runs; asking again in a second.");
            ShowLocked(false);
            if (!_disposed)
            {
                _askAbout = window;
                _askAgain.Change(AskAgainAfter, Timeout.InfiniteTimeSpan);
            }
        }
    }

    // True each time; false only after true, as only then something ends.
    internal void Show(bool running)
    {
        lock (_gate)
        {
            ShowLocked(running);
        }
    }

    private void ShowLocked(bool running)
    {
        if (!running && !_isRunning)
        {
            return;
        }

        _isRunning = running;
        Switched?.Invoke(this, running);
    }

    // Once, a second after a .scr that Windows did not confirm, on the thread of the timer, where a
    // throw would end the engine.
    private void AskAgain()
    {
        try
        {
            lock (_gate)
            {
                var window = _askAbout;
                _askAbout = 0;
                if (window == 0 || _disposed || _foreground() != window || !_windowsSaysRunning())
                {
                    return;
                }

                Trace.TraceInformation("Windows says a second later that the screen saver runs.");
                ShowLocked(true);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
        }
    }

    // A window that is gone, or whose program cannot be read, is no screen saver.
    private string? ReadProgram(nint window)
    {
        var processId = ForegroundHook.ProcessOf(window);
        return processId != 0 && _names.TryRead(processId, out var program) ? program : null;
    }

    private static bool WindowsSaysRunning()
    {
        if (SystemParametersInfo(SpiGetScreenSaverRunning, 0, out var running, 0))
        {
            return running != 0;
        }

        Trace.TraceWarning($"Whether the screen saver runs could not be asked (error {Marshal.GetLastPInvokeError()}).");
        return false;
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, out int value, uint winIni);
}
