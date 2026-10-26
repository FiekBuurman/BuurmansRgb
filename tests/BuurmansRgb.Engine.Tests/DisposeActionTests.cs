namespace BuurmansRgb.Engine.Tests;

public sealed class DisposeActionTests
{
    [Fact]
    public void Dispose_RunsTheActionOnce()
    {
        var runs = 0;
        var action = new DisposeAction(() => runs++);

        action.Dispose();
        action.Dispose();

        Assert.Equal(1, runs);
    }
}
