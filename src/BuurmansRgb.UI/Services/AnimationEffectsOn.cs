namespace BuurmansRgb.UI.Services;

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
