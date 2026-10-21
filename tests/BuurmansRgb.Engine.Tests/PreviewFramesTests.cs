using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class PreviewFramesTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<long> _sent = [];
    private readonly PreviewFrames _frames;
    private long _sequence;
    private int _copies;

    public PreviewFramesTests() => _frames = new PreviewFrames(GetOutput, output => _sent.Add(output.Sequence), _time);

    public void Dispose() => _frames.Dispose();

    // Through a timer that is due now: the system timer runs it on the thread pool, the fake clock at once.
    [Fact]
    public void Rendered_SendsTheLatestOutput()
    {
        Render();

        Assert.Equal([1L], _sent);
    }

    // The animation runs at 30 or 60 frames a second; the preview gets at most one per MinInterval,
    // and always the latest.
    [Fact]
    public void ManyRenders_SendsAtMostOnePerIntervalWithTheLatest()
    {
        Render();
        _time.Advance(TimeSpan.Zero);

        for (var i = 0; i < 5; i++)
        {
            Render();
            _time.Advance(TimeSpan.FromMilliseconds(10));
        }

        _time.Advance(PreviewFrames.MinInterval);

        Assert.Equal([1L, 6L], _sent);
        Assert.Equal(2, _copies);
    }

    [Fact]
    public void Rendered_LongAfterTheLastFrame_SendsRightAway()
    {
        Render();
        _time.Advance(TimeSpan.FromSeconds(1));

        Render();
        _time.Advance(TimeSpan.Zero);

        Assert.Equal([1L, 2L], _sent);
    }

    [Fact]
    public void NotWanted_CopiesAndSendsNothing()
    {
        _frames.Wanted = false;

        Render();
        Render();
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(_sent);
        Assert.Equal(0, _copies);
    }

    [Fact]
    public void NotWantedWhileAFrameWaits_DropsIt()
    {
        Render();
        Render();
        _frames.Wanted = false;

        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal([1L], _sent);
    }

    // The preview shows again with what the lights show now, also without a new render (static
    // lighting renders nothing).
    [Fact]
    public void WantedAgain_SendsTheLatestRightAway()
    {
        _frames.Wanted = false;
        Render();

        _frames.Wanted = true;
        _time.Advance(TimeSpan.Zero);

        Assert.Equal([1L], _sent);
    }

    [Fact]
    public void Wanted_TrueWhileAlreadyWanted_SendsNothing()
    {
        _frames.Wanted = true;
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(_sent);
    }

    [Fact]
    public void Dispose_StopsAFrameThatWaits()
    {
        Render();
        Render();

        _frames.Dispose();
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal([1L], _sent);
    }

    [Fact]
    public void SendThrows_KeepsSendingLaterFrames()
    {
        var calls = 0;
        using var frames = new PreviewFrames(GetOutput, _ =>
        {
            if (++calls == 1)
            {
                throw new IOException("The pipe closed.");
            }
        }, _time);

        frames.Rendered();
        _time.Advance(TimeSpan.Zero);
        frames.Rendered();
        _time.Advance(PreviewFrames.MinInterval);

        Assert.Equal(2, calls);
    }

    private void Render()
    {
        _sequence++;
        _frames.Rendered();
    }

    private LightingOutput GetOutput()
    {
        _copies++;
        return new LightingOutput(_sequence, []);
    }
}
