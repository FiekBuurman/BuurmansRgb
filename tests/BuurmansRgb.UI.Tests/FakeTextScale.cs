using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// A text size that a test sets, as Windows would.
internal sealed class FakeTextScale : ITextScale
{
    public event EventHandler? Changed;

    public double Factor { get; set; } = 1.0;

    public void Set(double factor)
    {
        Factor = factor;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
