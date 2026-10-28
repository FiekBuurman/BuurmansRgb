using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BuurmansRgb.UI.Tests;

// The CheckBox of the theme (Themes/Controls.xaml), in a window off screen.
[Collection(AppThemeCollection.Name)]
public sealed class CheckBoxStyleTests
{
    private const string Short = "Kort";
    private const string Long = "Kleuren over de LED's verdelen (uit: alle LED's tegelijk dezelfde kleur)";

    // One line sits in the middle of the box, as it did before a text could wrap; a text over more
    // lines keeps the box beside its first line, not between the lines.
    [Theory]
    [InlineData(Short, 400)]
    [InlineData(Long, 200)]
    public void Box_SitsBesideTheFirstLine(string content, double width)
    {
        AppTheme.Run(panel =>
        {
            var checkBox = new CheckBox { Content = content, Width = width };
            panel.Children.Add(checkBox);
            OffscreenWindow.WaitUntilIdle();
            var box = AppTheme.Descendants<Border>(checkBox).Single(border => border.Name == "Box");
            var text = AppTheme.Descendants<TextBlock>(checkBox).Single();
            var lineHeight = LineHeightOf(text);
            var boxMiddle = box.TranslatePoint(new Point(0, box.ActualHeight / 2), checkBox).Y;
            var firstLineMiddle = text.TranslatePoint(new Point(0, lineHeight / 2), checkBox).Y;

            Assert.Equal(content == Long, text.ActualHeight > lineHeight * 1.5);
            Assert.Equal(firstLineMiddle, boxMiddle, 1.0);
            Assert.Equal(checkBox.MinHeight / 2, boxMiddle, 0.5);
        });
    }

    private static double LineHeightOf(TextBlock text)
    {
        var line = new TextBlock
        {
            Text = "Kg",
            FontFamily = text.FontFamily,
            FontSize = text.FontSize,
            FontWeight = text.FontWeight,
            FontStyle = text.FontStyle,
        };
        line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return line.DesiredSize.Height;
    }
}
