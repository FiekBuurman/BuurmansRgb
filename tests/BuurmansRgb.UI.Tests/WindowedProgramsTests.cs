using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

public sealed class WindowedProgramsTests
{
    // What runs differs per pc; the form does not. The test process itself shows no window.
    [Fact]
    public void Find_GivesSortedExeNamesOnce()
    {
        var programs = WindowedPrograms.Find();

        Assert.All(programs, program => Assert.EndsWith(".exe", program, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(programs.Order(StringComparer.OrdinalIgnoreCase), programs);
        Assert.Equal(programs.Count, programs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
