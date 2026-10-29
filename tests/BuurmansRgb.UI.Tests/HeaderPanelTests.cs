using System.Windows;
using System.Windows.Controls;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class HeaderPanelTests
{
    [Theory]
    [InlineData(800, 400, 180, true)]
    [InlineData(580, 400, 180, true)]
    [InlineData(579, 400, 180, false)]
    [InlineData(685, 640, 360, false)]
    [InlineData(double.PositiveInfinity, 640, 360, true)]
    public void FitsSideBySide_WhileTheHeadingKeepsItsLeastWidth(double width, double asideWidth, double minHeadingWidth, bool expected)
    {
        Assert.Equal(expected, HeaderPanel.FitsSideBySide(width, asideWidth, minHeadingWidth));
    }

    // Side by side, the second child is on the right, as docked in a DockPanel; without the room it
    // goes under the heading, Gap below it, on the left.
    [Theory]
    [InlineData(600, true)]
    [InlineData(400, false)]
    public void Arrange_SideBySideOrUnder(double width, bool sideBySide) => Sta.Run(() =>
    {
        var heading = new Border { Height = 30 };
        var aside = new Border { Width = 250, Height = 44 };
        var panel = new HeaderPanel { MinHeadingWidth = 180, Gap = 12, Children = { heading, aside } };

        panel.Measure(new Size(width, double.PositiveInfinity));
        panel.Arrange(new Rect(panel.DesiredSize with { Width = width }));

        var place = aside.TranslatePoint(default, panel);
        if (sideBySide)
        {
            Assert.Equal(new Point(width - 250, 0), place);
            Assert.Equal(width - 250, heading.ActualWidth);
            Assert.Equal(44, panel.DesiredSize.Height);
        }
        else
        {
            Assert.Equal(new Point(0, 42), place);
            Assert.Equal(width, heading.ActualWidth);
            Assert.Equal(30 + 12 + 44, panel.DesiredSize.Height);
        }
    });
}
