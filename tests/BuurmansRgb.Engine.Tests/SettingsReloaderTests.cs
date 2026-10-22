using BuurmansRgb.Engine.Settings;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class SettingsReloaderTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(400);

    private readonly FakeSettingsFileEvents _fileEvents = new();
    private readonly InMemorySettingsStore _store = new();
    private readonly FakeTimeProvider _time = new();
    private readonly List<EngineSettings> _applied = [];

    [Fact]
    public void FileChangedByHand_IsAppliedAfterTheDelay()
    {
        using var reloader = CreateReloader();
        var settings = new EngineSettings { FramesPerSecond = 12 };
        _store.ChangeByHand(settings);

        _fileEvents.RaiseChanged();
        _time.Advance(Delay - TimeSpan.FromMilliseconds(1));
        Assert.Empty(_applied);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Same(settings, Assert.Single(_applied));
    }

    // An editor that writes the file in steps: read once, after the last one.
    [Fact]
    public void ManyQuickChanges_AreReadOnceAfterTheLast()
    {
        using var reloader = CreateReloader();
        _time.Advance(Delay);
        var before = _store.LoadIfChangedCount;
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });

        for (var i = 0; i < 10; i++)
        {
            _fileEvents.RaiseChanged();
            _time.Advance(Delay / 2);
        }

        Assert.Equal(before, _store.LoadIfChangedCount);
        _time.Advance(Delay);
        Assert.Equal(before + 1, _store.LoadIfChangedCount);
        Assert.Single(_applied);
    }

    // The engine's own save, or a file that does not read: the store has nothing new.
    [Fact]
    public void FileThatHoldsNothingNew_AppliesNothing()
    {
        using var reloader = CreateReloader();

        _fileEvents.RaiseChanged();
        _time.Advance(Delay);

        Assert.Empty(_applied);
        Assert.True(_store.LoadIfChangedCount > 0);
    }

    // Changed while the engine started, before the reloader listened.
    [Fact]
    public void AtTheStart_ReadsTheFileOnce()
    {
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });
        using var reloader = CreateReloader();

        _time.Advance(Delay);
        _time.Advance(Delay);

        Assert.Equal(1, _store.LoadIfChangedCount);
        Assert.Single(_applied);
    }

    // Without file events nothing is read: no polling.
    [Fact]
    public void WithoutChanges_ReadsNothingAfterTheStart()
    {
        using var reloader = CreateReloader();
        _time.Advance(Delay);

        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(1, _store.LoadIfChangedCount);
    }

    [Fact]
    public void ChangeAfterAnother_IsAppliedToo()
    {
        using var reloader = CreateReloader();
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });
        _fileEvents.RaiseChanged();
        _time.Advance(Delay);

        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 24 });
        _fileEvents.RaiseChanged();
        _time.Advance(Delay);

        Assert.Equal([12, 24], _applied.Select(settings => settings.FramesPerSecond));
    }

    // Held by another program a moment: read again after the delay, without another file event.
    [Fact]
    public void FileThatCannotBeReadNow_IsReadAgainAfterTheDelay()
    {
        using var reloader = CreateReloader();
        _time.Advance(Delay);
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });
        _store.HeldReads = 2;

        _fileEvents.RaiseChanged();
        _time.Advance(Delay);
        _time.Advance(Delay);
        Assert.Empty(_applied);

        _time.Advance(Delay);
        Assert.Equal(12, Assert.Single(_applied).FramesPerSecond);
    }

    // Held for good: a few more tries, then it waits for the next file event.
    [Fact]
    public void FileThatStaysHeld_IsReadAFewTimesAndThenNotAgain()
    {
        using var reloader = CreateReloader();
        _time.Advance(Delay);
        var before = _store.LoadIfChangedCount;
        _store.HeldReads = int.MaxValue;

        _fileEvents.RaiseChanged();
        for (var i = 0; i < 20; i++)
        {
            _time.Advance(Delay);
        }

        Assert.Equal(before + 6, _store.LoadIfChangedCount);
        _fileEvents.RaiseChanged();
        _time.Advance(Delay);
        Assert.Equal(before + 7, _store.LoadIfChangedCount);
    }

    // On a timer thread an exception would end the engine: logged, and the next change comes in.
    [Fact]
    public void ApplyThatThrows_IsLoggedAndTheNextChangeIsApplied()
    {
        var fail = true;
        using var reloader = new SettingsReloader(_fileEvents, _store, settings =>
        {
            if (fail)
            {
                throw new InvalidOperationException("Device gone.");
            }

            _applied.Add(settings);
        }, Delay, _time);
        _time.Advance(Delay);
        using var trace = new TraceCapture();

        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });
        _fileEvents.RaiseChanged();
        _time.Advance(Delay);
        Assert.Contains(trace.Messages, message => message.Contains("Device gone.", StringComparison.Ordinal));

        fail = false;
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 24 });
        _fileEvents.RaiseChanged();
        _time.Advance(Delay);
        Assert.Equal(24, Assert.Single(_applied).FramesPerSecond);
    }

    // Without a delay the first read may run at once, before the constructor is done: the timer
    // is there for a held file to be read again.
    [Fact]
    public void WithoutADelay_AHeldFileAtTheStart_IsReadAgain()
    {
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });
        _store.HeldReads = 1;

        using var reloader = new SettingsReloader(_fileEvents, _store, _applied.Add, TimeSpan.Zero, _time);
        _time.Advance(TimeSpan.Zero);

        Assert.Equal(12, Assert.Single(_applied).FramesPerSecond);
        Assert.True(_fileEvents.HasListeners);
    }

    [Fact]
    public void Dispose_StopsListeningAndApplyingAChangeThatWaits()
    {
        var reloader = CreateReloader();
        _time.Advance(Delay);
        _store.ChangeByHand(new EngineSettings { FramesPerSecond = 12 });
        _fileEvents.RaiseChanged();

        reloader.Dispose();
        _time.Advance(Delay);

        Assert.Empty(_applied);
        Assert.False(_fileEvents.HasListeners);
    }

    private SettingsReloader CreateReloader() => new(_fileEvents, _store, _applied.Add, Delay, _time);
}
