using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// What the devices show while Windows is locked or asleep, or the screen is off (PausedLights),
// and that the manual pause from the tray menu leaves them alone whatever it is.
public sealed class LightingControllerPausedLightsTests : IDisposable
{
    private static readonly RgbColor DimOrange = Orange.Scale(LightingController.DimmedBrightness);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly RecordingDeviceProvider _other = new("other-device");
    private readonly LightingController _controller;

    public LightingControllerPausedLightsTests() => _controller = new LightingController([_provider, _other], _time);

    public void Dispose() => _controller.Dispose();

    public static TheoryData<PauseReasons, PausedLightsMode> AutomaticReasonsAndModes => new()
    {
        { PauseReasons.Locked, PausedLightsMode.Dim },
        { PauseReasons.Locked, PausedLightsMode.Off },
        { PauseReasons.Asleep, PausedLightsMode.Dim },
        { PauseReasons.Asleep, PausedLightsMode.Off },
        { PauseReasons.DisplayOff, PausedLightsMode.Dim },
        { PauseReasons.DisplayOff, PausedLightsMode.Off },
    };

    [Fact]
    public void PausedLights_IsKeepByDefault() => Assert.Equal(PausedLightsMode.Keep, _controller.PausedLights);

    [Theory]
    [InlineData(PauseReasons.Locked)]
    [InlineData(PauseReasons.Asleep)]
    [InlineData(PauseReasons.DisplayOff)]
    public async Task Keep_LeavesTheLastFrameOnTheDevices(PauseReasons reason)
    {
        await StartAsync(EffectIds.Static);
        var sent = _provider.SetLedsCount;

        _controller.SetPaused(reason, true);

        Assert.Equal(sent, _provider.SetLedsCount);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Theory]
    [MemberData(nameof(AutomaticReasonsAndModes))]
    public async Task AutomaticReason_DimsOrBlacksOutEveryDevice_AndTheEndGivesTheColorsBack(PauseReasons reason, PausedLightsMode mode)
    {
        await StartAsync(EffectIds.Static);
        _controller.PausedLights = mode;
        var shown = Shown(mode);

        _controller.SetPaused(reason, true);
        Assert.Equal([shown, shown, shown], _provider.LastColors);
        Assert.Equal([shown, shown, shown], _other.LastColors);

        _controller.SetPaused(reason, false);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
        Assert.Equal([Orange, Orange, Orange], _other.LastColors);
    }

    // The buffers keep the real colors, so the preview in the settings app shows the lighting.
    [Fact]
    public async Task Off_KeepsTheRealColorsInTheOutput()
    {
        await StartAsync(EffectIds.Static);
        _controller.PausedLights = PausedLightsMode.Off;

        _controller.SetPaused(PauseReasons.Locked, true);

        Assert.Equal([Orange, Orange, Orange], _controller.GetOutput().Devices.Single(device => device.DeviceId == RecordingDeviceProvider.DeviceId).Colors);
    }

    [Fact]
    public async Task Dim_AnAnimatedEffect_DimsTheFrameItStoppedAt()
    {
        await StartAsync(EffectIds.Rainbow);
        _time.Advance(TimeSpan.FromMilliseconds(500));
        _controller.RenderAnimationFrame();
        var frame = _controller.GetOutput().Devices.Single(device => device.DeviceId == RecordingDeviceProvider.DeviceId).Colors;
        _controller.PausedLights = PausedLightsMode.Dim;

        _controller.SetPaused(PauseReasons.Locked, true);

        Assert.Equal(frame.Select(led => led.Scale(LightingController.DimmedBrightness)), _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task OneOfTwoAutomaticReasonsEnds_StaysBlack()
    {
        await StartAsync(EffectIds.Static);
        _controller.PausedLights = PausedLightsMode.Off;
        _controller.SetPaused(PauseReasons.Locked, true);
        _controller.SetPaused(PauseReasons.Asleep, true);

        _controller.SetPaused(PauseReasons.Asleep, false);

        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _provider.LastColors);
        Assert.Equal(PauseReasons.Locked, _controller.Pause);
    }

    [Fact]
    public async Task Manual_LeavesTheDevicesAsTheyAre_WhateverPausedLightsIs()
    {
        await StartAsync(EffectIds.Static);
        _controller.PausedLights = PausedLightsMode.Off;
        var sent = _provider.SetLedsCount;

        _controller.SetPaused(PauseReasons.Manual, true);
        _controller.SetPaused(PauseReasons.Locked, true);
        _controller.SetPaused(PauseReasons.Locked, false);

        Assert.Equal(sent, _provider.SetLedsCount);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    // The manual pause sends nothing, also not the real colors; when it ends while Windows is still
    // locked, the devices go black again, and after unlocking they get their colors.
    [Fact]
    public async Task ManualDuringALockThatBlackedOut_SendsNothingUntilItEnds()
    {
        await StartAsync(EffectIds.Static);
        _controller.PausedLights = PausedLightsMode.Off;
        _controller.SetPaused(PauseReasons.Locked, true);
        var sent = _provider.SetLedsCount;

        _controller.SetPaused(PauseReasons.Manual, true);
        Assert.Equal(sent, _provider.SetLedsCount);

        _controller.SetPaused(PauseReasons.Manual, false);
        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _provider.LastColors);

        _controller.SetPaused(PauseReasons.Locked, false);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Theory]
    [InlineData(PausedLightsMode.Dim)]
    [InlineData(PausedLightsMode.Off)]
    [InlineData(PausedLightsMode.Keep)]
    public async Task PausedLightsChanged_DuringALock_GoesOutAtOnce(PausedLightsMode mode)
    {
        await StartAsync(EffectIds.Static);
        _controller.PausedLights = mode == PausedLightsMode.Keep ? PausedLightsMode.Off : PausedLightsMode.Keep;
        _controller.SetPaused(PauseReasons.Locked, true);
        var shown = Shown(mode);

        _controller.PausedLights = mode;

        Assert.Equal([shown, shown, shown], _provider.LastColors);
    }

    [Fact]
    public async Task PausedLightsChanged_WhileRunning_SendsNothing()
    {
        await StartAsync(EffectIds.Static);
        var sent = _provider.SetLedsCount;

        _controller.PausedLights = PausedLightsMode.Off;

        Assert.Equal(sent, _provider.SetLedsCount);
    }

    [Fact]
    public async Task PausedLightsChanged_DuringAManualPause_SendsNothing()
    {
        await StartAsync(EffectIds.Static);
        _controller.SetPaused(PauseReasons.Manual, true);
        _controller.SetPaused(PauseReasons.Locked, true);
        var sent = _provider.SetLedsCount;

        _controller.PausedLights = PausedLightsMode.Off;

        Assert.Equal(sent, _provider.SetLedsCount);
    }

    [Fact]
    public void PausedLightsChanged_IsRaisedOnlyForAnotherValue()
    {
        var raised = 0;
        _controller.PausedLightsChanged += (_, _) => raised++;

        _controller.PausedLights = PausedLightsMode.Keep;
        _controller.PausedLights = PausedLightsMode.Dim;
        _controller.PausedLights = PausedLightsMode.Dim;

        Assert.Equal(1, raised);
    }

    // So the settings app and settings.json still get the new value.
    [Fact]
    public async Task PausedLightsChanged_ADeviceFailsWhileTheColorsGoOut_StillHoldsAndIsRaised()
    {
        await StartAsync(EffectIds.Static);
        _controller.SetPaused(PauseReasons.Locked, true);
        var raised = 0;
        _controller.PausedLightsChanged += (_, _) => raised++;
        _provider.ThrowOnNextSetLeds = true;

        Assert.Throws<InvalidOperationException>(() => _controller.PausedLights = PausedLightsMode.Off);

        Assert.Equal(PausedLightsMode.Off, _controller.PausedLights);
        Assert.Equal(1, raised);
    }

    // A scene that loads on time while Windows is locked stays dark, and shows after unlocking.
    [Fact]
    public async Task Off_AChangeWhileLocked_StaysBlackUntilUnlocked()
    {
        await StartAsync(EffectIds.Static);
        _controller.PausedLights = PausedLightsMode.Off;
        _controller.SetPaused(PauseReasons.Locked, true);

        _controller.Replace(Uniform(StaticLighting(Blue)));
        Assert.Equal([RgbColor.Black, RgbColor.Black, RgbColor.Black], _provider.LastColors);

        _controller.SetPaused(PauseReasons.Locked, false);
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
    }

    [Fact]
    public async Task Dim_RestoreDevicesWhileLocked_SendsTheDimmedFrame()
    {
        await StartAsync(EffectIds.Static);
        _controller.PausedLights = PausedLightsMode.Dim;
        _controller.SetPaused(PauseReasons.Locked, true);
        _provider.SetLeds(RecordingDeviceProvider.DeviceId, [RgbColor.White, RgbColor.White, RgbColor.White]);

        _controller.RestoreDevices();

        Assert.Equal([DimOrange, DimOrange, DimOrange], _provider.LastColors);
    }

    [Fact]
    public async Task Off_BeforeThereIsLighting_SendsNothing()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.PausedLights = PausedLightsMode.Off;

        _controller.SetPaused(PauseReasons.Locked, true);

        Assert.Equal(0, _provider.SetLedsCount);
    }

    private static RgbColor Shown(PausedLightsMode mode) => mode switch
    {
        PausedLightsMode.Dim => DimOrange,
        PausedLightsMode.Off => RgbColor.Black,
        _ => Orange,
    };

    private async Task StartAsync(string effectId)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(StaticLighting(Orange) with { EffectId = effectId }));
    }
}
