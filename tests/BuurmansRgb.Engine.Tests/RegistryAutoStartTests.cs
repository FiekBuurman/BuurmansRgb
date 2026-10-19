using BuurmansRgb.Engine.AutoStart;
using Microsoft.Win32;

namespace BuurmansRgb.Engine.Tests;

// Uses a throwaway key under HKCU\Software\BuurmansRgb.Tests, never the real Run key.
public sealed class RegistryAutoStartTests : IDisposable
{
    private const string ValueName = "BuurmansRgb";
    private const string Command = "\"C:\\Apps\\BuurmansRgb.Engine.exe\"";
    private const string TestRootKeyPath = @"Software\BuurmansRgb.Tests";

    private readonly string _keyPath = $@"{TestRootKeyPath}\{Guid.NewGuid():N}";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        using var root = Registry.CurrentUser.OpenSubKey(TestRootKeyPath);
        if (root is { SubKeyCount: 0, ValueCount: 0 })
        {
            Registry.CurrentUser.DeleteSubKey(TestRootKeyPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void IsEnabled_NoKey_IsFalse()
    {
        Assert.False(CreateAutoStart().IsEnabled);
    }

    [Fact]
    public void SetEnabled_True_WritesQuotedCommand()
    {
        var autoStart = CreateAutoStart();

        autoStart.SetEnabled(true);

        Assert.True(autoStart.IsEnabled);
        Assert.Equal(Command, ReadValue());
    }

    [Fact]
    public void SetEnabled_False_RemovesValue()
    {
        var autoStart = CreateAutoStart();
        autoStart.SetEnabled(true);

        autoStart.SetEnabled(false);

        Assert.False(autoStart.IsEnabled);
        Assert.Null(ReadValue());
    }

    [Fact]
    public void SetEnabled_FalseWithoutValue_DoesNotThrow()
    {
        CreateAutoStart().SetEnabled(false);
    }

    [Fact]
    public void IsEnabled_ValuePointsToOtherExecutable_IsFalse()
    {
        new RegistryAutoStart(_keyPath, ValueName, "\"C:\\Old\\BuurmansRgb.Engine.exe\"").SetEnabled(true);

        Assert.False(CreateAutoStart().IsEnabled);
    }

    private RegistryAutoStart CreateAutoStart() => new(_keyPath, ValueName, Command);

    private object? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(ValueName);
    }
}
