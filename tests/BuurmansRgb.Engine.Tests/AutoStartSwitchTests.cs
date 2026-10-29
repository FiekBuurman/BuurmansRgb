using System.Security;
using BuurmansRgb.Engine.AutoStart;

namespace BuurmansRgb.Engine.Tests;

// "Starten met Windows" as the tray menu and the settings apps share it, over a fake registry.
public sealed class AutoStartSwitchTests
{
    private readonly FakeAutoStart _autoStart = new();

    public static TheoryData<Exception> AccessErrors => [new UnauthorizedAccessException(), new SecurityException(), new IOException()];

    [Fact]
    public void SetEnabled_RaisesChangedOnlyForAnotherValue()
    {
        var autoStart = new AutoStartSwitch(_autoStart);
        var changes = 0;
        autoStart.Changed += (_, _) => changes++;

        autoStart.SetEnabled(true);
        autoStart.SetEnabled(true);
        autoStart.SetEnabled(false);

        Assert.False(autoStart.IsEnabled);
        Assert.Equal(2, changes);
    }

    // On for another engine counts as off for this one, so it writes each time.
    [Fact]
    public void SetEnabled_WritesAlsoWhenItIsOnAlready()
    {
        _autoStart.IsEnabled = true;
        var autoStart = new AutoStartSwitch(_autoStart);

        autoStart.SetEnabled(true);

        Assert.Equal(1, _autoStart.SetEnabledCount);
    }

    [Theory]
    [MemberData(nameof(AccessErrors))]
    public void TrySetEnabled_WhenWindowsRefuses_StaysAndLogsTheReason(Exception error)
    {
        using var trace = new TraceCapture();
        _autoStart.SetEnabledException = error;
        var autoStart = new AutoStartSwitch(_autoStart);
        var changes = 0;
        autoStart.Changed += (_, _) => changes++;

        Assert.False(autoStart.TrySetEnabled(true));

        Assert.False(autoStart.IsEnabled);
        Assert.Equal(0, changes);
        Assert.Contains(trace.Messages, message => message.Contains("Could not turn autostart on", StringComparison.Ordinal) && message.Contains(error.GetType().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void TrySetEnabled_Succeeds_ReturnsTrue()
    {
        var autoStart = new AutoStartSwitch(_autoStart);

        Assert.True(autoStart.TrySetEnabled(true));

        Assert.True(_autoStart.IsEnabled);
    }

    // Another failure is a bug, not something Windows refused, so it is not hidden.
    [Fact]
    public void TrySetEnabled_AnotherError_IsThrown()
    {
        _autoStart.SetEnabledException = new InvalidOperationException("bug");

        Assert.Throws<InvalidOperationException>(() => new AutoStartSwitch(_autoStart).TrySetEnabled(true));
    }

    [Theory]
    [MemberData(nameof(AccessErrors))]
    public void IsEnabled_Unreadable_IsOff(Exception error)
    {
        _autoStart.IsEnabled = true;
        _autoStart.IsEnabledException = error;

        Assert.False(new AutoStartSwitch(_autoStart).IsEnabled);
    }
}
