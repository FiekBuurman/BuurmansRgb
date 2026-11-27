namespace BuurmansRgb.UI.Services;

// Whether Windows shows animations (Instellingen → Toegankelijkheid → Visuele effecten →
// Animatie-effecten), and when that changes. Tests give a fake, so they never touch the setting
// of Windows.
public interface IAnimationEffects
{
    bool IsOn { get; }

    // On the UI thread, once IsOn has its new value.
    event EventHandler? Changed;
}
