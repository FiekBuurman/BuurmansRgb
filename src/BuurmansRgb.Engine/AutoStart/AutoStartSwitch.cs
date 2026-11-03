using System.Diagnostics;
using System.Security;

namespace BuurmansRgb.Engine.AutoStart;

// "Starten met Windows", for the tray menu and the settings apps together: both change it here,
// so Changed tells the settings apps (OptionsChanged) when the tray menu changed it, and the
// tray menu reads IsEnabled each time it opens, so it shows what a settings app set. Any thread.
internal sealed class AutoStartSwitch(IAutoStart autoStart)
{
    private readonly Lock _gate = new();

    // Raised after SetEnabled gave it another value, on the thread that set it; read IsEnabled for
    // the newest one.
    public event EventHandler? Changed;

    // Raised after SetEnabled wrote it, also to the value it had: the user chose, in the tray menu
    // or the settings app, so EngineHost keeps that in settings.json (autoStartChosen) and
    // --first-run leaves it from then on. Not raised by EnableOnFirstRun, nor after a failure.
    public event EventHandler? Chosen;

    // As Windows has it now; off when it cannot be read, which goes to the log.
    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return ReadEnabled();
            }
        }
    }

    // As IsEnabled, but null when it cannot be read, for EngineHost to tell nothing from that.
    public bool? TryReadEnabled()
    {
        lock (_gate)
        {
            return TryReadEnabledLocked();
        }
    }

    // Also writes when it is on already, as that may be for another engine (IAutoStart.IsEnabled
    // counts only this one). Throws UnauthorizedAccessException, SecurityException or IOException
    // when Windows does not let it change, such as a Run key without write access.
    public void SetEnabled(bool enabled)
    {
        bool changed;
        lock (_gate)
        {
            changed = ReadEnabled() != enabled;
            autoStart.SetEnabled(enabled);
        }

        Chosen?.Invoke(this, EventArgs.Empty);
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // For --first-run: on when it is off, as the installer asked and not the user, so without
    // Chosen. A failure only goes to the log (AutoStartExtensions.EnableOnFirstRun).
    public void EnableOnFirstRun()
    {
        bool changed;
        lock (_gate)
        {
            if (ReadEnabled())
            {
                return;
            }

            autoStart.EnableOnFirstRun();
            changed = ReadEnabled();
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // False when Windows did not let it change; the reason goes to the log, as the user only sees
    // the Dutch text of the caller.
    public bool TrySetEnabled(bool enabled)
    {
        try
        {
            SetEnabled(enabled);
            return true;
        }
        catch (Exception exception) when (IsAccessError(exception))
        {
            Trace.TraceError($"Could not turn autostart {(enabled ? "on" : "off")}: {exception}");
            return false;
        }
    }

    // What Windows throws when it does not let autostart be read or changed; AutoStartExtensions
    // catches the same.
    internal static bool IsAccessError(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException or IOException;

    private bool ReadEnabled() => TryReadEnabledLocked() ?? false;

    private bool? TryReadEnabledLocked()
    {
        try
        {
            return autoStart.IsEnabled;
        }
        catch (Exception exception) when (IsAccessError(exception))
        {
            Trace.TraceWarning($"Could not read whether autostart is on: {exception.Message}");
            return null;
        }
    }
}
