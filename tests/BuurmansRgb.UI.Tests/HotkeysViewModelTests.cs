using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class HotkeysViewModelTests
{
    private readonly HotkeysViewModel _hotkeys = new();

    [Fact]
    public void Enabled_IsOnAtFirst() => Assert.True(_hotkeys.Enabled);

    [Fact]
    public void Show_ShowsTheOptionAndRaisesNothing()
    {
        var raised = 0;
        _hotkeys.EnabledChanged += (_, _) => raised++;

        _hotkeys.Show(false);

        Assert.False(_hotkeys.Enabled);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Switching_RaisesTheNewValue()
    {
        var values = new List<bool>();
        _hotkeys.EnabledChanged += (_, value) => values.Add(value);

        _hotkeys.Enabled = false;
        _hotkeys.Enabled = true;

        Assert.Equal([false, true], values);
    }
}
