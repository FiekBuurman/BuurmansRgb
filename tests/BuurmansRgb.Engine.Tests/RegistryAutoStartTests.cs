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

    [Fact]
    public void FollowMove_OldEngineIsGone_PointsAutostartHere()
    {
        var gone = $"\"C:\\Gone-{Guid.NewGuid():N}\\BuurmansRgb.Engine.exe\"";
        new RegistryAutoStart(_keyPath, ValueName, gone).SetEnabled(true);
        var autoStart = CreateAutoStart();

        Assert.Equal(gone, autoStart.FollowMove());

        Assert.Equal(Command, ReadValue());
        Assert.True(autoStart.IsEnabled);
    }

    // Such as an installed engine next to a build: it keeps its autostart.
    [Fact]
    public void FollowMove_OtherEngineIsStillThere_LeavesIt()
    {
        var other = $"\"{Environment.ProcessPath}\"";
        new RegistryAutoStart(_keyPath, ValueName, other).SetEnabled(true);

        Assert.Null(CreateAutoStart().FollowMove());

        Assert.Equal(other, ReadValue());
    }

    [Fact]
    public void FollowMove_AutostartOffOrAlreadyHere_ChangesNothing()
    {
        var autoStart = CreateAutoStart();
        Assert.Null(autoStart.FollowMove());
        Assert.Null(ReadValue());

        autoStart.SetEnabled(true);
        Assert.Null(autoStart.FollowMove());
        Assert.Equal(Command, ReadValue());
    }

    private RegistryAutoStart CreateAutoStart() => new(_keyPath, ValueName, Command);

    private object? ReadValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(ValueName);
    }
}
