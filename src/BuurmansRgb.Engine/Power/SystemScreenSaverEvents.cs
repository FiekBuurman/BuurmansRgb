using System.Diagnostics;
using System.Runtime.InteropServices;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Power;

// Whether the screen saver runs, from the foreground window (ForegroundHook), without polling: a
// screen saver is a program with the extension .scr, and its window comes to the front when Windows
// starts it. SystemParametersInfo(SPI_GETSCREENSAVERRUNNING) confirms it, as Windows sets that only
// for a screen saver it starts itself: a .scr in front for "Voorbeeld" in the screen saver settings
// does not count. Asked only when a .scr comes to the front, so any other window costs one exe name.
// Whatever window comes to the front after it ends it, without asking Windows, which may still say
// it runs while the screen saver closes: the window the user was in, or the lock screen. Create and
// dispose it on the tray thread, like ForegroundHook.
internal sealed partial class SystemScreenSaverEvents : IScreenSaverEvents, IDisposable
{
    private const string ScreenSaverExtension = ".scr";
    private const uint SpiGetScreenSaverRunning = 0x0072;

    private readonly ForegroundHook _hook;
    private readonly Action<nint> _tell;
    private readonly ProcessImageName _names = new();
    private volatile bool _isRunning;

    // When Windows does not take the hook, the screen saver counts as not running, so the effects
    // run as they would without this. hook is shared with the rest of the engine and outlives this.
    public SystemScreenSaverEvents(ForegroundHook hook)
    {
        _hook = hook;
        _tell = Tell;
        if (!_hook.Listen(_tell))
        {
            Trace.TraceWarning("Whether the screen saver runs cannot be followed.");
            return;
        }

        // An engine that starts while the screen saver runs; Switched has no listeners yet.
        _isRunning = IsRunningScreenSaver(ForegroundHook.Current);
    }

    public event EventHandler<bool>? Switched;

    public bool IsRunning => _isRunning;

    public void Dispose() => _hook.Stop(_tell);

    // Whether program is a screen saver, by its exe name (any case).
    internal static bool IsScreenSaver(string? program) =>
        program is not null
        && program.Length > ScreenSaverExtension.Length
        && program.EndsWith(ScreenSaverExtension, StringComparison.OrdinalIgnoreCase);

    // A window that came to the front.
    internal void Tell(nint window) => Show(IsRunningScreenSaver(window));

    // True each time; false only after true, as only then something ends.
    internal void Show(bool running)
    {
        if (!running && !_isRunning)
        {
            return;
        }

        _isRunning = running;
        Switched?.Invoke(this, running);
    }

    // Windows is asked only for a .scr. A window that is gone, or whose program cannot be read, is no
    // screen saver.
    private bool IsRunningScreenSaver(nint window)
    {
        var processId = ForegroundHook.ProcessOf(window);
        return processId != 0
            && _names.TryRead(processId, out var program)
            && IsScreenSaver(program)
            && WindowsSaysRunning();
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
