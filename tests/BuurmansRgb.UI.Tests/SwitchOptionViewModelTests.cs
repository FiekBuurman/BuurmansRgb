using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class SwitchOptionViewModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsOn_StartsAsGiven(bool isOn) => Assert.Equal(isOn, new SwitchOptionViewModel(isOn).IsOn);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Show_ShowsTheOptionAndRaisesNothing(bool isOn)
    {
        var option = new SwitchOptionViewModel(isOn);
        var raised = 0;
        option.Switched += (_, _) => raised++;

        option.Show(!isOn);

        Assert.Equal(!isOn, option.IsOn);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Switching_RaisesTheNewValue()
    {
        var option = new SwitchOptionViewModel(isOn: false);
        var values = new List<bool>();
        option.Switched += (_, value) => values.Add(value);

        option.IsOn = true;
        option.IsOn = false;

        Assert.Equal([true, false], values);
    }
}
