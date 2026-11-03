using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// The memory and CPU of the engine on Instellingen, with a fake clock and a fake process: never a
// process of this pc, and the timer only while the page shows.
public sealed class EngineUsageViewModelTests
{
    private const int EngineId = 4242;
    private const long Megabyte = 1024 * 1024;

    private readonly FakeTimeProvider _time = new();
    private readonly FakeProcessMeter _meter = new();

    // Nothing is read while the page is not shown or the engine is not known.
    [Theory]
    [InlineData(false, EngineId)]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public void WithoutPageOrEngine_ReadsNothing(bool shown, int? processId)
    {
        using var usage = Create();

        usage.IsShown = shown;
        usage.ProcessId = processId;
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.False(usage.IsMeasuring);
        Assert.Empty(_meter.Reads);
        Assert.Equal(Strings.UsageUnknown, usage.Text);
    }

    // The memory at once as the page opens, the CPU with the next reading, every Interval.
    [Fact]
    public void Shown_ReadsAtOnceAndThenEveryInterval()
    {
        using var usage = Create(processorCount: 4);
        _meter.Next = new ProcessReading(TimeSpan.FromSeconds(10), 26 * Megabyte);
        usage.ProcessId = EngineId;

        usage.IsShown = true;

        Assert.True(usage.IsMeasuring);
        Assert.Equal([EngineId], _meter.Reads);
        Assert.Equal(UiText.Format(Strings.UsageMemoryOnly, 26.0), usage.Text);

        // 80 ms of CPU in 2 s on 4 cores: 1 % of the pc.
        _meter.Next = new ProcessReading(TimeSpan.FromSeconds(10.08), 27 * Megabyte);
        _time.Advance(EngineUsageViewModel.Interval);

        Assert.Equal(2, _meter.Reads.Count);
        Assert.Equal(UiText.Format(Strings.Usage, 27.0, 1.0), usage.Text);

        _time.Advance(EngineUsageViewModel.Interval);
        Assert.Equal(3, _meter.Reads.Count);
        Assert.Equal(UiText.Format(Strings.Usage, 27.0, 0.0), usage.Text);
    }

    // Leaving the page, minimizing (MainViewModel says not shown) or losing the engine stops it.
    [Fact]
    public void Hidden_StopsTheTimer()
    {
        using var usage = Create();
        usage.ProcessId = EngineId;
        usage.IsShown = true;

        usage.IsShown = false;
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.False(usage.IsMeasuring);
        Assert.Single(_meter.Reads);
        Assert.Equal(Strings.UsageUnknown, usage.Text);
    }

    [Fact]
    public void EngineGone_StopsTheTimer()
    {
        using var usage = Create();
        usage.ProcessId = EngineId;
        usage.IsShown = true;

        usage.ProcessId = null;
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.False(usage.IsMeasuring);
        Assert.Single(_meter.Reads);
    }

    // Back on the page the CPU starts over: no share over the time the page was away.
    [Fact]
    public void ShownAgain_StartsOver()
    {
        using var usage = Create();
        usage.ProcessId = EngineId;
        usage.IsShown = true;
        usage.IsShown = false;
        _time.Advance(TimeSpan.FromMinutes(1));

        usage.IsShown = true;

        Assert.Equal(2, _meter.Reads.Count);
        Assert.Equal(UiText.Format(Strings.UsageMemoryOnly, 26.0), usage.Text);
    }

    // A process that is gone or cannot be read says so, and the next reading starts over.
    [Fact]
    public void Unreadable_SaysSo()
    {
        using var usage = Create();
        usage.ProcessId = EngineId;
        usage.IsShown = true;

        _meter.Next = null;
        _time.Advance(EngineUsageViewModel.Interval);

        Assert.Equal(Strings.UsageUnknown, usage.Text);
    }

    // Closing the app stops the timer for good.
    [Fact]
    public void Dispose_StopsTheTimer()
    {
        var usage = Create();
        usage.ProcessId = EngineId;
        usage.IsShown = true;

        usage.Dispose();
        _time.Advance(TimeSpan.FromMinutes(1));
        usage.IsShown = false;
        usage.IsShown = true;

        Assert.Single(_meter.Reads);
        Assert.True(_meter.IsDisposed);
    }

    private EngineUsageViewModel Create(int processorCount = 1) => new(_meter, _time, processorCount);

    private sealed class FakeProcessMeter : IProcessMeter, IDisposable
    {
        public ProcessReading? Next { get; set; } = new(TimeSpan.FromSeconds(1), 26 * Megabyte);

        public List<int> Reads { get; } = [];

        public bool IsDisposed { get; private set; }

        public ProcessReading? Read(int processId)
        {
            Reads.Add(processId);
            return Next;
        }

        public void Dispose() => IsDisposed = true;
    }
}
