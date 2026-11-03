using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

public sealed class SettingsFilesTests
{
    [Fact]
    public void DefaultFilePaths_AreTheFilesOfABackup()
    {
        Assert.Equal(SettingsFiles.Default.PathOf(SettingsBackup.SettingsFileName), JsonSettingsStore.DefaultFilePath);
        Assert.Equal(SettingsFiles.Default.PathOf(SettingsBackup.ProfilesFileName), JsonProfileStore.DefaultFilePath);
        Assert.Equal(SettingsFiles.Default.PathOf(SettingsBackup.EffectsFileName), JsonCustomEffectStore.DefaultFilePath);
    }
}
