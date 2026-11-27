using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// "Animatie-effecten" that a test turns on and off, as Windows would.
internal sealed class FakeAnimationEffects : IAnimationEffects
{
    public event EventHandler? Changed;

    public bool IsOn { get; set; } = true;

    public bool HasListeners => Changed is not null;

    public void Turn(bool on)
    {
        IsOn = on;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
