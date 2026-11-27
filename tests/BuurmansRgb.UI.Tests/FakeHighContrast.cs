using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// A contrast theme that a test turns on and off, as Windows would.
internal sealed class FakeHighContrast : IHighContrast
{
    public event EventHandler? Changed;

    public bool IsOn { get; set; }

    public void Turn(bool on)
    {
        IsOn = on;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
