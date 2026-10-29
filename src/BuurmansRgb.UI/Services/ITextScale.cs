namespace BuurmansRgb.UI.Services;

// The text size of Windows (Instellingen → Toegankelijkheid → Tekstgrootte), as a factor from 1.0
// to 2.25, and when it changes. Tests give a fake, so they never touch the setting of Windows.
public interface ITextScale
{
    double Factor { get; }

    // On the UI thread, once Factor has its new value.
    event EventHandler? Changed;
}
