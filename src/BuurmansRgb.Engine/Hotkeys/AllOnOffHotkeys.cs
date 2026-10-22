namespace BuurmansRgb.Engine.Hotkeys;

// The fixed hotkeys of "Alles aan" and "Alles uit" in the tray menu, and whether the engine uses
// them. A fixed key can clash with another app, so they can be turned off; the tray registers
// them (TrayPresenter.ApplyHotkeys).
internal sealed class AllOnOffHotkeys
{
    // A for "aan", U for "uit". With all three modifiers, so they are not likely to be taken.
    public const Keys AllOn = Keys.Control | Keys.Alt | Keys.Shift | Keys.A;
    public const Keys AllOff = Keys.Control | Keys.Alt | Keys.Shift | Keys.U;

    private readonly Lock _gate = new();
    private bool _enabled = true;

    // Raised when Enabled got another value, on the thread that set it; read Enabled for the
    // newest one.
    public event EventHandler? EnabledChanged;

    // On unless settings.json or the settings app turns it off.
    public bool Enabled
    {
        get
        {
            lock (_gate)
            {
                return _enabled;
            }
        }
        set
        {
            bool changed;
            lock (_gate)
            {
                changed = _enabled != value;
                _enabled = value;
            }

            if (changed)
            {
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // As Windows writes it in a menu, such as "Ctrl+Alt+Shift+A".
    public static string DisplayText(Keys key) =>
        string.Concat(
            key.HasFlag(Keys.Control) ? "Ctrl+" : "",
            key.HasFlag(Keys.Alt) ? "Alt+" : "",
            key.HasFlag(Keys.Shift) ? "Shift+" : "",
            (key & Keys.KeyCode).ToString());
}
