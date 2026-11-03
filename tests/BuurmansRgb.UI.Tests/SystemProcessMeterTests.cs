using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// The real meter, only on the process of this test itself: it reads, it changes nothing.
public sealed class SystemProcessMeterTests
{
    [Fact]
    public void Read_OwnProcess_GivesItsCpuAndMemory()
    {
        using var meter = new SystemProcessMeter();

        var first = meter.Read(Environment.ProcessId);
        var second = meter.Read(Environment.ProcessId);

        Assert.NotNull(first);
        Assert.True(first.Value.WorkingSetBytes > 0);
        Assert.True(second!.Value.ProcessorTime >= first.Value.ProcessorTime);
    }

    // A process id that is not there (an engine that stopped) gives nothing, and no exception.
    [Fact]
    public void Read_ProcessThatIsNotThere_GivesNothing()
    {
        using var meter = new SystemProcessMeter();

        Assert.Null(meter.Read(int.MaxValue - 1));
    }
}
