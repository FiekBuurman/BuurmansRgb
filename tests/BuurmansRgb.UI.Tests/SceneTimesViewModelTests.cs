using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class SceneTimesViewModelTests
{
    private readonly SceneTimesViewModel _sceneTimes = new();

    [Fact]
    public void CatchUp_IsOffAtFirst() => Assert.False(_sceneTimes.CatchUp);

    [Fact]
    public void Show_ShowsTheOptionAndRaisesNothing()
    {
        var raised = 0;
        _sceneTimes.CatchUpChanged += (_, _) => raised++;

        _sceneTimes.Show(true);

        Assert.True(_sceneTimes.CatchUp);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Switching_RaisesTheNewValue()
    {
        var values = new List<bool>();
        _sceneTimes.CatchUpChanged += (_, value) => values.Add(value);

        _sceneTimes.CatchUp = true;
        _sceneTimes.CatchUp = false;

        Assert.Equal([true, false], values);
    }
}
