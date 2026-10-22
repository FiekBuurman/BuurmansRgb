using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakeSettingsFileEvents : ISettingsFileEvents
{
    public event EventHandler? Changed;

    public bool HasListeners => Changed is not null;

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
