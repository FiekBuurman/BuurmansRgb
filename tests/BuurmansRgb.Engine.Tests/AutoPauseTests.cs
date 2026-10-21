using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Win32;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// With the events of Windows from fakes and a real LightingController, so the tests see whether
// the animation timer runs.
public sealed class AutoPauseTests : IDisposable
{
    private readonly FakePowerEvents _power = new();
    private readonly FakeSessionEvents _session = new();
    private readonly FakeDisplayEvents _display = new();
    private readonly FakeTimeProvider _time = new();
    private readonly RestorableDeviceProvider _restorable = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _lighting;
    private readonly AutoPause _autoPause;

    public AutoPauseTests()
    {
        _lighting = new LightingController([_restorable, _provider], _time);
        _autoPause = new AutoPause(_power, _session, _display, _lighting.SetPaused);
    }

    public void Dispose()
    {
        _autoPause.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public async Task SessionLock_StopsTheAnimationAndUnlockRunsItAgain()
    {
        await StartAnimatedAsync();

        _session.Raise(SessionSwitchReason.SessionLock);
        Assert.Equal(PauseReasons.Locked, _lighting.Pause);
        Assert.False(_lighting.IsAnimating);

        _session.Raise(SessionSwitchReason.SessionUnlock);
        Assert.Equal(PauseReasons.None, _lighting.Pause);
        Assert.True(_lighting.IsAnimating);
    }

    [Theory]
    [InlineData(SessionSwitchReason.ConsoleDisconnect, SessionSwitchReason.ConsoleConnect)]
    [InlineData(SessionSwitchReason.ConsoleDisconnect, SessionSwitchReason.RemoteConnect)]
    [InlineData(SessionSwitchReason.RemoteDisconnect, SessionSwitchReason.ConsoleConnect)]
    public async Task Disconnected_PausesUntilTheSessionIsOnAScreenAgain(SessionSwitchReason disconnect, SessionSwitchReason connect)
    {
        await StartAnimatedAsync();

        _session.Raise(disconnect);
        Assert.Equal(PauseReasons.Locked, _lighting.Pause);

        _session.Raise(connect);
        Assert.True(_lighting.IsAnimating);
    }

    // Switching users locks the session and then disconnects it; coming back goes the other way.
    // The engine of the other user drives the same devices meanwhile.
    [Fact]
    public async Task BackAfterSwitchingUsers_GivesTheDevicesTheirOwnLightingAgain()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(Uniform(StaticLighting(Orange)));
        _session.Raise(SessionSwitchReason.ConsoleDisconnect);
        _provider.SetLeds(RecordingDeviceProvider.DeviceId, [RgbColor.White, RgbColor.White, RgbColor.White]);

        _session.Raise(SessionSwitchReason.ConsoleConnect);

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public async Task LockedAndDisconnected_RunsOnlyWhenBothEnd()
    {
        await StartAnimatedAsync();
        _session.Raise(SessionSwitchReason.SessionLock);
        _session.Raise(SessionSwitchReason.ConsoleDisconnect);

        _session.Raise(SessionSwitchReason.ConsoleConnect);
        Assert.False(_lighting.IsAnimating);

        _session.Raise(SessionSwitchReason.SessionUnlock);
        Assert.True(_lighting.IsAnimating);
    }

    [Theory]
    [InlineData(SessionSwitchReason.SessionLogon)]
    [InlineData(SessionSwitchReason.SessionLogoff)]
    [InlineData(SessionSwitchReason.SessionRemoteControl)]
    public async Task OtherSessionChanges_ChangeNothing(SessionSwitchReason reason)
    {
        await StartAnimatedAsync();

        _session.Raise(reason);

        Assert.Equal(PauseReasons.None, _lighting.Pause);
        Assert.True(_lighting.IsAnimating);
    }

    // An engine that starts late after sign-in may start on the lock screen.
    [Fact]
    public async Task StartedWhileLocked_PausesUntilUnlocked()
    {
        await StartAnimatedAsync();
        var session = new FakeSessionEvents { IsLocked = true };

        using var autoPause = new AutoPause(_power, session, new FakeDisplayEvents(), _lighting.SetPaused);
        Assert.Equal(PauseReasons.Locked, _lighting.Pause);

        session.Raise(SessionSwitchReason.SessionUnlock);
        Assert.True(_lighting.IsAnimating);
    }

    [Fact]
    public async Task Suspending_StopsTheAnimationUntilResumed()
    {
        await StartAnimatedAsync();

        _power.RaiseSuspending();
        Assert.Equal(PauseReasons.Asleep, _lighting.Pause);
        Assert.False(_lighting.IsAnimating);

        _power.RaiseResumed();
        Assert.True(_lighting.IsAnimating);
    }

    // Windows can wake up without a Resume from SystemEvents; the user coming back ends the sleep.
    [Theory]
    [InlineData(SessionSwitchReason.SessionUnlock)]
    [InlineData(SessionSwitchReason.ConsoleConnect)]
    [InlineData(SessionSwitchReason.RemoteConnect)]
    public async Task SuspendingWithoutResume_EndsWhenTheUserIsBack(SessionSwitchReason back)
    {
        await StartAnimatedAsync();
        _power.RaiseSuspending();

        _session.Raise(back);

        Assert.Equal(PauseReasons.None, _lighting.Pause);
        Assert.True(_lighting.IsAnimating);
    }

    [Fact]
    public async Task DisplayOff_StopsTheAnimationUntilItIsOnAgain()
    {
        await StartAnimatedAsync();

        _display.Raise(off: true);
        Assert.Equal(PauseReasons.DisplayOff, _lighting.Pause);
        Assert.False(_lighting.IsAnimating);

        _display.Raise(off: false);
        Assert.Equal(PauseReasons.None, _lighting.Pause);
        Assert.True(_lighting.IsAnimating);
    }

    // As after unlocking: the devices get their lighting again when the screen goes on.
    [Fact]
    public async Task DisplayOn_GivesTheDevicesTheirLightingAgain()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(Uniform(StaticLighting(Orange)));
        _display.Raise(off: true);
        _provider.SetLeds(RecordingDeviceProvider.DeviceId, [RgbColor.White, RgbColor.White, RgbColor.White]);

        _display.Raise(off: false);

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    // Windows tells the state right after the engine starts listening, which may be before AutoPause is made.
    [Fact]
    public async Task StartedWithTheScreenOff_PausesUntilItIsOn()
    {
        await StartAnimatedAsync();
        var display = new FakeDisplayEvents { IsOff = true };

        using var autoPause = new AutoPause(_power, _session, display, _lighting.SetPaused);
        Assert.Equal(PauseReasons.DisplayOff, _lighting.Pause);

        display.Raise(off: false);
        Assert.True(_lighting.IsAnimating);
    }

    // Should Windows not tell that the screen is on again, the user who is back still sees the effects.
    [Theory]
    [InlineData(SessionSwitchReason.SessionUnlock)]
    [InlineData(SessionSwitchReason.ConsoleConnect)]
    [InlineData(SessionSwitchReason.RemoteConnect)]
    public async Task DisplayOffWithoutOn_EndsWhenTheUserIsBack(SessionSwitchReason back)
    {
        await StartAnimatedAsync();
        _display.Raise(off: true);

        _session.Raise(back);

        Assert.Equal(PauseReasons.None, _lighting.Pause);
        Assert.True(_lighting.IsAnimating);
    }

    [Fact]
    public async Task Unlock_KeepsTheManualPause()
    {
        await StartAnimatedAsync();
        _lighting.SetPaused(PauseReasons.Manual, true);
        _session.Raise(SessionSwitchReason.SessionLock);

        _session.Raise(SessionSwitchReason.SessionUnlock);

        Assert.Equal(PauseReasons.Manual, _lighting.Pause);
        Assert.False(_lighting.IsAnimating);
    }

    // Windows usually locks before it sleeps and wakes up to the lock screen. The devices get their
    // lighting back right then, the animation only after unlocking.
    [Fact]
    public async Task WakeUpToTheLockScreen_RestoresTheDevicesAndRunsAfterUnlocking()
    {
        using var restorer = new ResumeRestorer(_power, _lighting.RestoreDevices, _time, ResumeRestorer.DefaultDelays);
        await StartAnimatedAsync();
        _session.Raise(SessionSwitchReason.SessionLock);
        _power.RaiseSuspending();
        _restorable.Calls.Clear();

        _power.RaiseResumed();
        _time.Advance(ResumeRestorer.DefaultDelays[0]);

        Assert.Equal(PauseReasons.Locked, _lighting.Pause);
        Assert.Equal(["leds", "restore", "leds"], _restorable.Calls);
        Assert.False(_lighting.IsAnimating);
        _session.Raise(SessionSwitchReason.SessionUnlock);
        Assert.True(_lighting.IsAnimating);
    }

    [Fact]
    public async Task DeviceFailsWhileLocking_StillPausesWithoutThrowing()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(Uniform(StaticLighting(Orange)));
        _lighting.Flash(RecordingDeviceProvider.DeviceId);
        _provider.ThrowOnNextSetLeds = true;
        var changes = new List<PauseReasons>();
        _lighting.PauseChanged += (_, pause) => changes.Add(pause);

        _session.Raise(SessionSwitchReason.SessionLock);

        Assert.Equal(PauseReasons.Locked, _lighting.Pause);
        Assert.False(_lighting.IsAnimating);
        Assert.Equal([PauseReasons.Locked], changes);
    }

    [Fact]
    public async Task Disabled_IgnoresLockSleepAndTheScreen()
    {
        await StartAnimatedAsync();
        _autoPause.Enabled = false;

        _session.Raise(SessionSwitchReason.SessionLock);
        _power.RaiseSuspending();
        _display.Raise(off: true);

        Assert.Equal(PauseReasons.None, _lighting.Pause);
        Assert.True(_lighting.IsAnimating);
    }

    [Fact]
    public async Task DisabledWhileLocked_EndsThePauseAndEnabledStartsItAgain()
    {
        await StartAnimatedAsync();
        _session.Raise(SessionSwitchReason.SessionLock);

        _autoPause.Enabled = false;
        Assert.True(_lighting.IsAnimating);

        _autoPause.Enabled = true;
        Assert.Equal(PauseReasons.Locked, _lighting.Pause);
    }

    [Fact]
    public async Task DisabledWhileTheScreenIsOff_EndsThePauseAndEnabledStartsItAgain()
    {
        await StartAnimatedAsync();
        _display.Raise(off: true);

        _autoPause.Enabled = false;
        Assert.True(_lighting.IsAnimating);

        _autoPause.Enabled = true;
        Assert.Equal(PauseReasons.DisplayOff, _lighting.Pause);
    }

    [Fact]
    public void EnabledChanged_RaisedOnlyForAnotherValue()
    {
        var seen = new List<bool>();
        _autoPause.EnabledChanged += (_, _) => seen.Add(_autoPause.Enabled);

        _autoPause.Enabled = true;
        _autoPause.Enabled = false;
        _autoPause.Enabled = false;
        _autoPause.Enabled = true;

        Assert.Equal([false, true], seen);
    }

    // Away is for what stops whenever nobody is at the pc, whether the effects pause or not.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Away_FollowsLockAndSleep_AlsoWhenDisabled(bool enabled)
    {
        _autoPause.Enabled = enabled;
        var changes = new List<PauseReasons>();
        _autoPause.AwayChanged += (_, away) => changes.Add(away);

        _session.Raise(SessionSwitchReason.SessionLock);
        _power.RaiseSuspending();
        Assert.Equal(PauseReasons.Locked | PauseReasons.Asleep, _autoPause.Away);

        _power.RaiseResumed();
        _session.Raise(SessionSwitchReason.SessionUnlock);
        Assert.Equal(PauseReasons.None, _autoPause.Away);
        Assert.Equal([PauseReasons.Locked, PauseReasons.Locked | PauseReasons.Asleep, PauseReasons.Locked, PauseReasons.None], changes);
    }

    [Theory]
    [InlineData(SessionSwitchReason.ConsoleDisconnect, SessionSwitchReason.ConsoleConnect)]
    [InlineData(SessionSwitchReason.RemoteDisconnect, SessionSwitchReason.RemoteConnect)]
    public void Away_DisconnectedUntilConnected_AlsoWhenDisabled(SessionSwitchReason disconnect, SessionSwitchReason connect)
    {
        _autoPause.Enabled = false;

        _session.Raise(disconnect);
        Assert.Equal(PauseReasons.Locked, _autoPause.Away);

        _session.Raise(connect);
        Assert.Equal(PauseReasons.None, _autoPause.Away);
    }

    // Windows can wake up without a Resume from SystemEvents; the user coming back ends the sleep.
    [Theory]
    [InlineData(SessionSwitchReason.SessionUnlock)]
    [InlineData(SessionSwitchReason.ConsoleConnect)]
    [InlineData(SessionSwitchReason.RemoteConnect)]
    public void Away_SuspendingWithoutResume_EndsWhenTheUserIsBack_AlsoWhenDisabled(SessionSwitchReason back)
    {
        _autoPause.Enabled = false;
        _power.RaiseSuspending();
        Assert.Equal(PauseReasons.Asleep, _autoPause.Away);

        _session.Raise(back);

        Assert.Equal(PauseReasons.None, _autoPause.Away);
    }

    // Somebody may still be at the pc with the screen off, listening to music.
    [Fact]
    public void Away_NotForTheScreen()
    {
        var changes = new List<PauseReasons>();
        _autoPause.AwayChanged += (_, away) => changes.Add(away);

        _display.Raise(off: true);
        Assert.Equal(PauseReasons.DisplayOff, _lighting.Pause);
        Assert.Equal(PauseReasons.None, _autoPause.Away);

        _session.Raise(SessionSwitchReason.SessionLock);
        _display.Raise(off: false);
        Assert.Equal([PauseReasons.Locked], changes);
    }

    [Fact]
    public void AwayChanged_RaisedOnlyForAnotherValue_AndNotForEnabled()
    {
        var changes = new List<PauseReasons>();
        _autoPause.AwayChanged += (_, away) => changes.Add(away);

        _session.Raise(SessionSwitchReason.SessionLock);
        _session.Raise(SessionSwitchReason.ConsoleDisconnect);
        _autoPause.Enabled = false;
        _session.Raise(SessionSwitchReason.SessionLock);
        _autoPause.Enabled = true;

        Assert.Equal([PauseReasons.Locked], changes);
    }

    [Fact]
    public async Task AwayChangedListenerFails_StillPausesAndIsAway_WithoutThrowing()
    {
        await StartAnimatedAsync();
        using var trace = new TraceCapture();
        _autoPause.AwayChanged += (_, _) => throw new InvalidOperationException("The away listener failed.");

        _session.Raise(SessionSwitchReason.SessionLock);

        Assert.Equal(PauseReasons.Locked, _autoPause.Away);
        Assert.Equal(PauseReasons.Locked, _lighting.Pause);
        Assert.Contains(trace.Messages, message => message.Contains("The away listener failed.", StringComparison.Ordinal));
    }

    [Fact]
    public void StartedWhileLocked_IsAwayAtOnce()
    {
        using var autoPause = new AutoPause(_power, new FakeSessionEvents { IsLocked = true }, new FakeDisplayEvents(), _lighting.SetPaused) { Enabled = false };

        Assert.Equal(PauseReasons.Locked, autoPause.Away);
    }

    [Fact]
    public async Task Dispose_StopsListening()
    {
        await StartAnimatedAsync();

        _autoPause.Dispose();
        _session.Raise(SessionSwitchReason.SessionLock);
        _display.Raise(off: true);

        Assert.False(_power.HasListeners);
        Assert.False(_session.HasListeners);
        Assert.False(_display.HasListeners);
        Assert.True(_lighting.IsAnimating);
    }

    // Series of events as Windows can give them; the animation runs only when nothing pauses it.
    [Theory]
    [InlineData("resume", PauseReasons.None)]
    [InlineData("unlock", PauseReasons.None)]
    [InlineData("lock lock unlock", PauseReasons.None)]
    [InlineData("suspend suspend resume", PauseReasons.None)]
    [InlineData("suspend resume resume", PauseReasons.None)]
    [InlineData("lock suspend resume", PauseReasons.Locked)]
    [InlineData("lock suspend unlock resume", PauseReasons.None)]
    [InlineData("lock suspend resume unlock", PauseReasons.None)]
    [InlineData("lock disconnect unlock", PauseReasons.Locked)]
    [InlineData("disconnect connect connect", PauseReasons.None)]
    [InlineData("manual suspend resume", PauseReasons.Manual)]
    [InlineData("manual lock suspend unlock", PauseReasons.Manual)]
    [InlineData("lock manual unlock unmanual", PauseReasons.None)]
    [InlineData("on", PauseReasons.None)]
    [InlineData("off off on", PauseReasons.None)]
    [InlineData("off lock", PauseReasons.Locked | PauseReasons.DisplayOff)]
    [InlineData("off lock on", PauseReasons.Locked)]
    [InlineData("lock off on unlock", PauseReasons.None)]
    [InlineData("lock off suspend resume", PauseReasons.Locked | PauseReasons.DisplayOff)]
    [InlineData("lock off suspend resume on", PauseReasons.Locked)]
    [InlineData("lock off suspend on resume unlock", PauseReasons.None)]
    [InlineData("off suspend resume on", PauseReasons.None)]
    [InlineData("off disconnect on", PauseReasons.Locked)]
    [InlineData("off unlock off", PauseReasons.DisplayOff)]
    [InlineData("off connect off", PauseReasons.DisplayOff)]
    [InlineData("manual off on", PauseReasons.Manual)]
    [InlineData("off manual on unmanual", PauseReasons.None)]
    public async Task EventSeries_EndInTheRightPause(string events, PauseReasons pause)
    {
        await StartAnimatedAsync();

        foreach (var name in events.Split(' '))
        {
            Raise(name);
        }

        Assert.Equal(pause, _lighting.Pause);
        Assert.Equal(pause == PauseReasons.None, _lighting.IsAnimating);
    }

    // The restore after waking up waits for the pause from the tray menu, also through the sleep.
    [Fact]
    public async Task SleepDuringAManualPause_TheRestoreWaitsForTheManualPause()
    {
        using var restorer = new ResumeRestorer(_power, _lighting.RestoreDevices, _time, ResumeRestorer.DefaultDelays);
        await StartAnimatedAsync();
        _lighting.SetPaused(PauseReasons.Manual, true);
        _power.RaiseSuspending();
        _restorable.Calls.Clear();

        _power.RaiseResumed();
        _time.Advance(ResumeRestorer.DefaultDelays[0]);
        Assert.Empty(_restorable.Calls);

        _lighting.SetPaused(PauseReasons.Manual, false);
        Assert.Equal(["restore", "leds"], _restorable.Calls);
    }

    // With "off" the devices are black while any reason besides the manual one holds, and get
    // their colors back when the last one ends; the manual pause leaves them as they were.
    [Theory]
    [InlineData("lock", true)]
    [InlineData("suspend", true)]
    [InlineData("off", true)]
    [InlineData("lock unlock", false)]
    [InlineData("suspend resume", false)]
    [InlineData("off on", false)]
    [InlineData("lock suspend resume", true)]
    [InlineData("lock suspend resume unlock", false)]
    [InlineData("suspend unlock", false)]
    [InlineData("off lock on", true)]
    [InlineData("off lock on unlock", false)]
    [InlineData("disconnect connect", false)]
    [InlineData("manual", false)]
    [InlineData("manual lock suspend", false)]
    [InlineData("manual lock unlock unmanual", false)]
    [InlineData("lock manual unmanual", true)]
    public async Task Off_EventSeries_BlackWhileAnAutomaticReasonHolds(string events, bool black)
    {
        await StartStaticAsync();
        _lighting.PausedLights = PausedLightsMode.Off;

        foreach (var name in events.Split(' '))
        {
            Raise(name);
        }

        Assert.Equal(black ? [RgbColor.Black, RgbColor.Black, RgbColor.Black] : [Orange, Orange, Orange], _provider.LastColors);
    }

    // The devices are black before Windows sleeps, so lamps that keep power do not stay on; the
    // restores after waking up keep them black on the lock screen, and unlocking brings the colors.
    [Fact]
    public async Task Off_Sleep_BlackBeforeSleepingAndAfterWaking_ColorsAfterUnlocking()
    {
        using var restorer = new ResumeRestorer(_power, _lighting.RestoreDevices, _time, ResumeRestorer.DefaultDelays);
        await StartStaticAsync();
        _lighting.PausedLights = PausedLightsMode.Off;
        _session.Raise(SessionSwitchReason.SessionLock);

        var blackBySuspending = false;
        _power.Suspending += (_, _) => blackBySuspending = _provider.LastColors.All(led => led == RgbColor.Black);
        _power.RaiseSuspending();
        Assert.True(blackBySuspending);

        _power.RaiseResumed();
        _provider.SetLeds(RecordingDeviceProvider.DeviceId, [RgbColor.White, RgbColor.White, RgbColor.White]);
        _time.Advance(ResumeRestorer.DefaultDelays[0]);
        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _provider.LastColors);

        _session.Raise(SessionSwitchReason.SessionUnlock);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    private async Task StartStaticAsync()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(Uniform(StaticLighting(Orange)));
    }

    private async Task StartAnimatedAsync()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(Uniform(StaticLighting(Orange) with { EffectId = EffectIds.Rainbow }));
    }

    // "manual" and "unmanual" are the pause from the tray menu, "off" and "on" the screen.
    private void Raise(string name)
    {
        switch (name)
        {
            case "lock":
                _session.Raise(SessionSwitchReason.SessionLock);
                break;
            case "unlock":
                _session.Raise(SessionSwitchReason.SessionUnlock);
                break;
            case "disconnect":
                _session.Raise(SessionSwitchReason.ConsoleDisconnect);
                break;
            case "connect":
                _session.Raise(SessionSwitchReason.ConsoleConnect);
                break;
            case "suspend":
                _power.RaiseSuspending();
                break;
            case "resume":
                _power.RaiseResumed();
                break;
            case "off" or "on":
                _display.Raise(off: name == "off");
                break;
            case "manual" or "unmanual":
                _lighting.SetPaused(PauseReasons.Manual, name == "manual");
                break;
            default:
                throw new ArgumentException($"Unknown event '{name}'.", nameof(name));
        }
    }
}
