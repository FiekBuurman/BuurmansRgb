using BuurmansRgb.Engine.AutoStart;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakeAutoStart : IAutoStart
{
    public Exception? SetEnabledException { get; set; }

    public int SetEnabledCount { get; private set; }

    public bool IsEnabled { get; set; }

    public void SetEnabled(bool enabled)
    {
        SetEnabledCount++;
        if (SetEnabledException is not null)
        {
            throw SetEnabledException;
        }

        IsEnabled = enabled;
    }
}
