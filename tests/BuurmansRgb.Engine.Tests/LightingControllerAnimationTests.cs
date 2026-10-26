using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerAnimationTests : IDisposable
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / LightingController.DefaultFramesPerSecond);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerAnimationTests() => _controller = new LightingController([_provider], _time);

    [Fact]
    public void FramesPerSecondChanged_RaisedOnlyForAnotherValueWithinTheRange()
    {
        var seen = new List<int>();
        _controller.FramesPerSecondChanged += (_, _) => seen.Add(_controller.FramesPerSecond);

        _controller.FramesPerSecond = LightingController.DefaultFramesPerSecond;
        _controller.FramesPerSecond = 60;
        _controller.FramesPerSecond = 500;
        _controller.FramesPerSecond = 0;

        Assert.Equal([60, 1], seen);
    }

    public void Dispose() => _controller.Dispose();

    [Fact]
    public async Task Apply_StaticEffect_RunsNoTimer()
    {
        await _controller.DiscoverAsync(Ct);

        _controller.Apply(Uniform(EffectIds.Static));

        Assert.False(_controller.IsAnimating);
    }

    [Theory]
    [InlineData(EffectIds.Breathing)]
    [InlineData(EffectIds.Rainbow)]
    [InlineData(EffectIds.Wave)]
    [InlineData(EffectIds.Twinkle)]
    [InlineData(EffectIds.Comet)]
    [InlineData(EffectIds.Scanner)]
    [InlineData(EffectIds.Fire)]
    public async Task Apply_AnimatedEffect_StartsTheAnimation(string effectId)
    {
        await _controller.DiscoverAsync(Ct);

        _controller.Apply(Uniform(effectId));

        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task SetLighting_BackToStatic_StopsTheAnimation()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));

        _controller.SetLighting(null, lighting => lighting with { EffectId = EffectIds.Static });

        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public void Apply_WithoutDevices_RunsNoTimer()
    {
        using var controller = new LightingController([], _time);

        controller.Apply(Uniform(EffectIds.Rainbow));

        Assert.False(controller.IsAnimating);
    }

    [Fact]
    public async Task Timer_SendsAFramePerTick()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));
        var first = _provider.LastColors;

        _time.Advance(Frame);

        await Eventually.TrueAsync(() => _provider.SetLedsCount == 2);
        Assert.NotEqual(first, _provider.LastColors);
    }

    [Fact]
    public async Task Timer_RaisesRenderedWithAGrowingSequence()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));
        var before = _controller.GetOutput().Sequence;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _controller.Rendered += (_, _) => rendered.TrySetResult();

        _time.Advance(Frame);

        await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.True(_controller.GetOutput().Sequence > before);
    }

    [Fact]
    public async Task Timer_ProviderThrowsOnce_KeepsAnimating()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));
        _provider.ThrowOnNextSetLeds = true;

        _time.Advance(Frame);
        await Eventually.TrueAsync(() => !_provider.ThrowOnNextSetLeds);
        _time.Advance(Frame);

        await Eventually.TrueAsync(() => _provider.SetLedsCount == 2);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task Timer_RenderedListenerThrowsOnce_KeepsAnimating()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));
        var frames = 0;
        _controller.Rendered += (_, _) =>
        {
            if (Interlocked.Increment(ref frames) == 1)
            {
                throw new InvalidOperationException("The listener failed.");
            }
        };

        _time.Advance(Frame);
        await Eventually.TrueAsync(() => Volatile.Read(ref frames) == 1);
        _time.Advance(Frame);

        await Eventually.TrueAsync(() => Volatile.Read(ref frames) == 2);
        Assert.Equal(3, _provider.SetLedsCount);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task SetPaused_StopsTheAnimationAndResumingStartsIt()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));

        _controller.SetPaused(PauseReasons.Manual, true);
        Assert.False(_controller.IsAnimating);

        _controller.SetPaused(PauseReasons.Manual, false);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task Apply_WhilePaused_RunsNoTimer()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetPaused(PauseReasons.Manual, true);

        _controller.Apply(Uniform(EffectIds.Rainbow));

        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task RenderAnimationFrame_NothingChanged_SendsNothing()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));
        var rendered = 0;
        _controller.Rendered += (_, _) => rendered++;

        // The fake clock did not move, so the rainbow is where it was.
        _controller.RenderAnimationFrame();

        Assert.Equal(1, _provider.SetLedsCount);
        Assert.Equal(0, rendered);
    }

    [Fact]
    public async Task RenderAnimationFrame_SendsOnlyTheAnimatedDevices()
    {
        var animated = new RecordingDeviceProvider("animated");
        var still = new RecordingDeviceProvider("still");
        var time = new SteppingTimeProvider(Frame);
        using var controller = new LightingController([animated, still], time);
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.WithDevices(
            new DeviceLighting(Orange, 1.0, EffectIds.Static),
            new Dictionary<string, DeviceLighting> { ["animated"] = new(Orange, 1.0, EffectIds.Rainbow) }));

        controller.RenderAnimationFrame();

        Assert.Equal(2, animated.SetLedsCount);
        Assert.Equal(1, still.SetLedsCount);
    }

    // With a listener that does nothing, as with the settings app closed; an open settings app
    // copies every frame for its preview. Every effect that moves, as each renders its own way, and
    // the effects with a list of colors, and the color loop, also with four and with twelve, the most.
    [Theory]
    [InlineData(EffectIds.Rainbow, 0)]
    [InlineData(EffectIds.Twinkle, 0)]
    [InlineData(EffectIds.Comet, 0)]
    [InlineData(EffectIds.Scanner, 0)]
    [InlineData(EffectIds.Fire, 0)]
    [InlineData(EffectIds.ColorLoop, 0)]
    [InlineData(EffectIds.Twinkle, 4)]
    [InlineData(EffectIds.Comet, 4)]
    [InlineData(EffectIds.Scanner, 4)]
    [InlineData(EffectIds.Fire, 4)]
    [InlineData(EffectIds.ColorLoop, 4)]
    [InlineData(EffectIds.Twinkle, 12)]
    [InlineData(EffectIds.Comet, 12)]
    [InlineData(EffectIds.Scanner, 12)]
    [InlineData(EffectIds.Fire, 12)]
    [InlineData(EffectIds.ColorLoop, 12)]
    public async Task RenderAnimationFrame_WithoutASettingsApp_AllocatesNothing(string effectId, int colorCount)
    {
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], new SteppingTimeProvider(Frame));
        await controller.DiscoverAsync(Ct);
        var lighting = new DeviceLighting(Orange, 0.5, effectId);
        RgbColor[] colors = [.. Twelve.Take(colorCount)];
        controller.Apply(LightingStates.Uniform(colorCount == 0 ? lighting
            : effectId == EffectIds.ColorLoop ? lighting with { Loop = new ColorLoop(colors) }
            : lighting with { EffectColors = EffectColors.None.With(effectId, colors) }));
        controller.Rendered += (_, _) => { };
        controller.RenderAnimationFrame();

        var allocated = Allocations.During(() =>
        {
            for (var frame = 0; frame < 300; frame++)
            {
                controller.RenderAnimationFrame();
            }
        });

        Assert.Equal(0, allocated);
        Assert.Contains(simulated.GetLeds("simulated-keyboard"), led => led != RgbColor.Black);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(45, 45)]
    [InlineData(500, LightingController.MaxFramesPerSecond)]
    public void FramesPerSecond_IsKeptInRange(int requested, int expected)
    {
        _controller.FramesPerSecond = requested;

        Assert.Equal(expected, _controller.FramesPerSecond);
    }

    // At the default 30 fps, half a frame later no tick would come yet.
    [Fact]
    public async Task FramesPerSecond_WhileAnimating_ChangesTheRunningTimer()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));

        _controller.FramesPerSecond = LightingController.MaxFramesPerSecond;
        _time.Advance(TimeSpan.FromSeconds(1.0 / LightingController.MaxFramesPerSecond));

        await Eventually.TrueAsync(() => _provider.SetLedsCount == 2);
    }

    [Fact]
    public async Task DevicesChanged_WhileAnimating_AnimatesTheNewDevices()
    {
        var dynamic = new DynamicDeviceProvider();
        using var controller = new LightingController([dynamic], _time);
        await controller.DiscoverAsync(Ct);
        controller.Apply(Uniform(EffectIds.Rainbow));
        var devicesChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.DevicesChanged += (_, _) => devicesChanged.TrySetResult();

        dynamic.SetDevices(DynamicDeviceProvider.Device("new"));
        await devicesChanged.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        var first = dynamic.Leds["new"];
        _time.Advance(Frame);

        await Eventually.TrueAsync(() => !ReferenceEquals(first, dynamic.Leds["new"]));
        Assert.NotEqual(first, dynamic.Leds["new"]);
        Assert.True(controller.IsAnimating);
    }

    [Fact]
    public async Task Dispose_StopsTheAnimationForGood()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));

        _controller.Dispose();
        _controller.SetLighting(null, lighting => lighting with { EffectId = EffectIds.Wave });

        Assert.False(_controller.IsAnimating);
    }

    private static LightingState Uniform(string effectId, double brightness = 1.0) =>
        LightingStates.Uniform(new DeviceLighting(Orange, brightness, effectId));
}
