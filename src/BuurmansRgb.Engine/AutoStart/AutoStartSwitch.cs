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

    private static bool IsAccessError(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException or IOException;

    private bool ReadEnabled()
    {
        try
        {
            return autoStart.IsEnabled;
        }
        catch (Exception exception) when (IsAccessError(exception))
        {
            Trace.TraceWarning($"Could not read whether autostart is on: {exception.Message}");
            return false;
        }
    }
}
