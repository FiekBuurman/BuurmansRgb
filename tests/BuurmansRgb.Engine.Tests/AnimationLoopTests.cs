using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class AnimationLoopTests : IDisposable
{
    private static readonly TimeSpan Period = TimeSpan.FromSeconds(0.1);

    private readonly FakeTimeProvider _time = new();
    private readonly AnimationLoop _loop;
    private int _frames;
    private bool _throwOnce;

    public AnimationLoopTests() => _loop = new AnimationLoop(_time, Period, RenderFrame);

    private int Frames => Volatile.Read(ref _frames);

    public void Dispose() => _loop.Dispose();

    [Fact]
    public void NotStarted_DoesNotRun()
    {
        Assert.False(_loop.IsRunning);
    }

    [Fact]
    public async Task Start_RendersAFramePerPeriod()
    {
        _loop.Start();

        _time.Advance(Period);
        await Eventually.TrueAsync(() => Frames == 1);
        _time.Advance(Period);

        await Eventually.TrueAsync(() => Frames == 2);
        Assert.True(_loop.IsRunning);
    }

    [Fact]
    public async Task Start_Twice_RunsOneTimer()
    {
        _loop.Start();
        _loop.Start();

        _time.Advance(Period);
        await Eventually.TrueAsync(() => Frames == 1);
        _time.Advance(Period);
        await Eventually.TrueAsync(() => Frames == 2);

        Assert.Equal(2, Frames);
    }

    [Fact]
    public void Stop_EndsTheLoop()
    {
        _loop.Start();

        _loop.Stop();
        _time.Advance(Period);

        Assert.False(_loop.IsRunning);
        Assert.Equal(0, Frames);
    }

    [Fact]
    public async Task Period_WhileRunning_ChangesTheTimer()
    {
        _loop.Start();

        _loop.Period = Period / 2;
        _time.Advance(Period / 2);

        await Eventually.TrueAsync(() => Frames == 1);
    }

    [Fact]
    public async Task FrameThatThrows_DoesNotStopTheLoop()
    {
        _throwOnce = true;
        _loop.Start();

        _time.Advance(Period);
        await Eventually.TrueAsync(() => Frames == 1);
        _time.Advance(Period);

        await Eventually.TrueAsync(() => Frames == 2);
        Assert.True(_loop.IsRunning);
    }

    [Fact]
    public void Period_ZeroOrLess_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _loop.Period = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationLoop(_time, -Period, RenderFrame));
    }

    private void RenderFrame()
    {
        Interlocked.Increment(ref _frames);
        if (_throwOnce)
        {
            _throwOnce = false;
            throw new InvalidOperationException("The frame failed.");
        }
    }
}
