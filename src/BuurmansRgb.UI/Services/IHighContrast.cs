namespace BuurmansRgb.UI.Services;

// Whether Windows has a contrast theme on (Instellingen → Toegankelijkheid → Contrastthema's), and
// when that, or the theme, changes. Tests give a fake, so they never touch the setting of Windows.
public interface IHighContrast
{
    bool IsOn { get; }

    // On the UI thread: on, off, or another contrast theme.
    event EventHandler? Changed;
}
