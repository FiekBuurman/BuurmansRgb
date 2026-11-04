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

    // Spelled out, so a renamed constant shows up here and not only on the user's disk.
    [Fact]
    public void DefaultFilePaths_AreInAppDataBuurmansRgb()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BuurmansRgb");
        Assert.Equal(Path.Combine(folder, "settings.json"), JsonSettingsStore.DefaultFilePath);
        Assert.Equal(Path.Combine(folder, "profiles.json"), JsonProfileStore.DefaultFilePath);
        Assert.Equal(Path.Combine(folder, "effects.json"), JsonCustomEffectStore.DefaultFilePath);
    }

    // In UTC with milliseconds, so the names sort as they came.
    [Fact]
    public void BackupName_IsUtcWithMilliseconds() =>
        Assert.Equal("before-restore-20261103-221500-007.zip", SettingsFiles.BackupName(new DateTime(2026, 11, 3, 22, 15, 0, 7, DateTimeKind.Utc)));
}
