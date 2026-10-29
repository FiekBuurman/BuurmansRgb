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

// Animations always on: for a MainViewModel without the setting of Windows, such as in a test
// that is not about it.
public sealed class AnimationEffectsOn : IAnimationEffects
{
    public static AnimationEffectsOn Instance { get; } = new();

    private AnimationEffectsOn()
    {
    }

    public bool IsOn => true;

    // Never raised: the setting never changes.
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}
