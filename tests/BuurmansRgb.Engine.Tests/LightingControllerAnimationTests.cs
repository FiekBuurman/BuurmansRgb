using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerAnimationTests : IDisposable
{
    private static readonly RgbColor Orange = new(255, 136, 0);
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / LightingController.DefaultFramesPerSecond);

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerAnimationTests() => _controller = new LightingController([_provider], _time);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
    public async Task SetPaused_StopsTheAnimationAndResumingStartsIt()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(Uniform(EffectIds.Rainbow));

        _controller.SetPaused(true);
        Assert.False(_controller.IsAnimating);

        _controller.SetPaused(false);
        Assert.True(_controller.IsAnimating);
    }

    [Fact]
    public async Task Apply_WhilePaused_RunsNoTimer()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetPaused(true);

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
        controller.Apply(new LightingState(
            new DeviceLighting(Orange, 1.0, EffectIds.Static),
            new Dictionary<string, DeviceLighting> { ["animated"] = new(Orange, 1.0, EffectIds.Rainbow) }));

        controller.RenderAnimationFrame();

        Assert.Equal(2, animated.SetLedsCount);
        Assert.Equal(1, still.SetLedsCount);
    }

    [Fact]
    public async Task RenderAnimationFrame_AllocatesNothing()
    {
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], new SteppingTimeProvider(Frame));
        await controller.DiscoverAsync(Ct);
        controller.Apply(Uniform(EffectIds.Rainbow, brightness: 0.5));
        controller.Rendered += (_, _) => { };
        controller.RenderAnimationFrame();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 300; frame++)
        {
            controller.RenderAnimationFrame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.NotEqual(RgbColor.Black, simulated.GetLeds("simulated-keyboard")[0]);
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
