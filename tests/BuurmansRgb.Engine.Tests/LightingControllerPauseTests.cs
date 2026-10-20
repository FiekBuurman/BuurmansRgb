using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// The reasons to pause next to each other: the manual pause from the tray menu, and the pauses
// while Windows is locked or asleep.
public sealed class LightingControllerPauseTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly RestorableDeviceProvider _restorable = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;
    private readonly List<PauseReasons> _changes = [];

    public LightingControllerPauseTests()
    {
        _controller = new LightingController([_restorable, _provider], _time);
        _controller.PauseChanged += (_, pause) => _changes.Add(pause);
    }

    public void Dispose() => _controller.Dispose();

    [Theory]
    [InlineData(PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked)]
    [InlineData(PauseReasons.Asleep)]
    public async Task AnyReason_StopsTheAnimationAndTheEndStartsIt(PauseReasons reason)
    {
        await StartAsync(EffectIds.Rainbow);

        _controller.SetPaused(reason, true);
        Assert.False(_controller.IsAnimating);
        Assert.Equal(reason, _controller.Pause);

        _controller.SetPaused(reason, false);
        Assert.True(_controller.IsAnimating);
        Assert.Equal(PauseReasons.None, _controller.Pause);
    }

    [Fact]
    public async Task Locked_LeavesTheLastFrameOnTheDevices()
    {
        await StartAsync(EffectIds.Rainbow);
        var sent = _provider.SetLedsCount;

        _controller.SetPaused(PauseReasons.Locked, true);

        Assert.Equal(sent, _provider.SetLedsCount);
    }

    [Fact]
    public async Task Locked_AChangeStillReachesTheDevices()
    {
        await StartAsync(EffectIds.Rainbow);
        _controller.SetPaused(PauseReasons.Locked, true);

        _controller.Replace(Uniform(StaticLighting(Orange)));

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Unlocked_DuringAManualPause_KeepsTheDevicesAsTheyAre()
    {
        await StartAsync(EffectIds.Rainbow);
        _controller.SetPaused(PauseReasons.Manual, true);
        _controller.SetPaused(PauseReasons.Locked, true);

        _controller.SetPaused(PauseReasons.Locked, false);
        var sent = _provider.SetLedsCount;
        _controller.Apply(Uniform(StaticLighting(Orange)));

        Assert.Equal(PauseReasons.Manual, _controller.Pause);
        Assert.False(_controller.IsAnimating);
        Assert.Equal(sent, _provider.SetLedsCount);
    }

    [Fact]
    public async Task ManualPauseEnds_WhileLocked_SendsTheLightingButKeepsTheAnimationStopped()
    {
        await StartAsync(EffectIds.Rainbow);
        _controller.SetPaused(PauseReasons.Manual, true);
        _controller.Apply(Uniform(StaticLighting(Orange) with { EffectId = EffectIds.Breathing }));
        _controller.SetPaused(PauseReasons.Locked, true);
        var sent = _provider.SetLedsCount;

        _controller.SetPaused(PauseReasons.Manual, false);

        Assert.Equal(PauseReasons.Locked, _controller.Pause);
        Assert.Equal(sent + 1, _provider.SetLedsCount);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task RestoreDevices_WhileLocked_RestoresAtOnce()
    {
        await StartAsync(EffectIds.Static);
        _controller.SetPaused(PauseReasons.Locked, true);

        _controller.RestoreDevices();

        Assert.Equal(["leds", "restore", "leds"], _restorable.Calls);
    }

    [Fact]
    public async Task RestoreDevices_WhileManuallyPausedAndLocked_WaitsForTheManualPauseOnly()
    {
        await StartAsync(EffectIds.Static);
        _controller.SetPaused(PauseReasons.Manual, true);
        _controller.SetPaused(PauseReasons.Locked, true);
        _controller.RestoreDevices();

        _controller.SetPaused(PauseReasons.Locked, false);
        Assert.Equal(["leds"], _restorable.Calls);

        _controller.SetPaused(PauseReasons.Manual, false);
        Assert.Equal(["leds", "restore", "leds"], _restorable.Calls);
    }

    [Theory]
    [InlineData(PauseReasons.Locked)]
    [InlineData(PauseReasons.Asleep)]
    public async Task Flash_WhileLockedOrAsleep_DoesNothing(PauseReasons reason)
    {
        await StartAsync(EffectIds.Static);
        _controller.SetPaused(reason, true);
        var sent = _provider.SetLedsCount;

        _controller.Flash(RecordingDeviceProvider.DeviceId);

        Assert.Equal(sent, _provider.SetLedsCount);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Locked_WhileFlashing_GivesTheOwnColorsBack()
    {
        await StartAsync(EffectIds.Static);
        _controller.Flash(RecordingDeviceProvider.DeviceId);

        _controller.SetPaused(PauseReasons.Locked, true);

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public void PauseChanged_GivesTheReasonsAfterEachChange()
    {
        _controller.SetPaused(PauseReasons.Locked, true);
        _controller.SetPaused(PauseReasons.Manual, true);
        _controller.SetPaused(PauseReasons.Locked, false);
        _controller.SetPaused(PauseReasons.Manual, false);

        Assert.Equal([PauseReasons.Locked, PauseReasons.Locked | PauseReasons.Manual, PauseReasons.Manual, PauseReasons.None], _changes);
    }

    [Fact]
    public void PauseChanged_NothingChanges_IsNotRaised()
    {
        _controller.SetPaused(PauseReasons.Asleep, false);
        _controller.SetPaused(PauseReasons.Locked, true);
        _controller.SetPaused(PauseReasons.Locked, true);

        Assert.Equal([PauseReasons.Locked], _changes);
    }

    private async Task StartAsync(string effectId)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(StaticLighting(Orange) with { EffectId = effectId }));
    }
}
