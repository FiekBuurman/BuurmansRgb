using BuurmansRgb.Engine.SettingsApp;

namespace BuurmansRgb.Engine.Tests;

public sealed class SettingsAppLauncherTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void TryLaunch_ExecutableMissing_ReturnsFalse()
    {
        var launcher = new SettingsAppLauncher(_directory.File(SettingsAppLauncher.ExecutableName));

        Assert.False(launcher.TryLaunch());
    }
}
