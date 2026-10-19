using BuurmansRgb.Engine.SettingsApp;

namespace BuurmansRgb.Engine.Tests;

public sealed class SettingsAppLauncherTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Launch_ExecutableMissing_ReturnsNotFound()
    {
        var launcher = new SettingsAppLauncher(_directory.File(SettingsAppLauncher.ExecutableName));

        Assert.Equal(SettingsAppLaunchResult.NotFound, launcher.Launch());
    }

    [Fact]
    public void Launch_FileIsNotAnExecutable_ReturnsFailed()
    {
        var path = _directory.File(SettingsAppLauncher.ExecutableName);
        File.WriteAllText(path, "not a Windows executable");

        Assert.Equal(SettingsAppLaunchResult.Failed, new SettingsAppLauncher(path).Launch());
    }
}
