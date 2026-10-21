using System.Windows.Forms;
using BuurmansRgb.Engine.Hotkeys;

namespace BuurmansRgb.Engine.Tests;

public sealed class AllOnOffHotkeysTests
{
    [Fact]
    public void Enabled_IsOnAtFirst() => Assert.True(new AllOnOffHotkeys().Enabled);

    [Fact]
    public void Enabled_RaisesEnabledChangedOnlyForAnotherValue()
    {
        var hotkeys = new AllOnOffHotkeys();
        var changes = 0;
        hotkeys.EnabledChanged += (_, _) => changes++;

        hotkeys.Enabled = true;
        hotkeys.Enabled = false;
        hotkeys.Enabled = false;

        Assert.False(hotkeys.Enabled);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(AllOnOffHotkeys.AllOn, "Ctrl+Alt+Shift+A")]
    [InlineData(AllOnOffHotkeys.AllOff, "Ctrl+Alt+Shift+U")]
    [InlineData(Keys.Control | Keys.F9, "Ctrl+F9")]
    public void DisplayText_WritesTheKeyAsAMenuDoes(Keys key, string text) =>
        Assert.Equal(text, AllOnOffHotkeys.DisplayText(key));
}
