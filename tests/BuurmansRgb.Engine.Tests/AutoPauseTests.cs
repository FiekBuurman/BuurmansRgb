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
    private readonly FakeTimeProvider _time = new();
    private readonly RestorableDeviceProvider _restorable = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _lighting;
    private readonly AutoPause _autoPause;

    public AutoPauseTests()
    {
        _lighting = new LightingController([_restorable, _provider], _time);
        _autoPause = new AutoPause(_power, _session, _lighting.SetPaused);
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
    public async Task Disabled_IgnoresLockAndSleep()
    {
        await StartAnimatedAsync();
        _autoPause.Enabled = false;

        _session.Raise(SessionSwitchReason.SessionLock);
        _power.RaiseSuspending();

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
    public async Task Dispose_StopsListening()
    {
        await StartAnimatedAsync();

        _autoPause.Dispose();
        _session.Raise(SessionSwitchReason.SessionLock);

        Assert.False(_power.HasListeners);
        Assert.False(_session.HasListeners);
        Assert.True(_lighting.IsAnimating);
    }

    private async Task StartAnimatedAsync()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.Apply(Uniform(StaticLighting(Orange) with { EffectId = EffectIds.Rainbow }));
    }
}
