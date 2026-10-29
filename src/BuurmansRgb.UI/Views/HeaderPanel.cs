using System.Windows;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// A heading (the first child) with what belongs to it on its right (the second), such as the choice
// of the lamp on Kleur: side by side while the heading keeps at least MinHeadingWidth beside it, as a
// DockPanel with the second docked right would; otherwise the second goes under the heading, Gap
// below it. So a larger text size of Windows, which makes both wider, does not squeeze the heading
// away. Children after the second are not laid out.
public sealed class HeaderPanel : Panel
{
    public static readonly DependencyProperty MinHeadingWidthProperty = DependencyProperty.Register(
        nameof(MinHeadingWidth), typeof(double), typeof(HeaderPanel), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(HeaderPanel), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private bool _sideBySide = true;

    public double MinHeadingWidth
    {
        get => (double)GetValue(MinHeadingWidthProperty);
        set => SetValue(MinHeadingWidthProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    // Whether the heading keeps its least width beside the second child; always with an unknown width.
    internal static bool FitsSideBySide(double width, double asideWidth, double minHeadingWidth) =>
        !double.IsFinite(width) || width - asideWidth >= minHeadingWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var (heading, aside) = HeadingAndAside();
        if (heading is null)
        {
            return default;
        }

        if (aside is null)
        {
            heading.Measure(availableSize);
            return heading.DesiredSize;
        }

        aside.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        _sideBySide = FitsSideBySide(availableSize.Width, aside.DesiredSize.Width, MinHeadingWidth);
        if (_sideBySide)
        {
            heading.Measure(new Size(Math.Max(0, availableSize.Width - aside.DesiredSize.Width), availableSize.Height));
            return new Size(heading.DesiredSize.Width + aside.DesiredSize.Width, Math.Max(heading.DesiredSize.Height, aside.DesiredSize.Height));
        }

        heading.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        return new Size(Math.Max(heading.DesiredSize.Width, aside.DesiredSize.Width), heading.DesiredSize.Height + Gap + aside.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (heading, aside) = HeadingAndAside();
        if (aside is null)
        {
            heading?.Arrange(new Rect(finalSize));
        }
        else if (_sideBySide)
        {
            var asideWidth = Math.Min(aside.DesiredSize.Width, finalSize.Width);
            heading!.Arrange(new Rect(0, 0, finalSize.Width - asideWidth, finalSize.Height));
            aside.Arrange(new Rect(finalSize.Width - asideWidth, 0, asideWidth, finalSize.Height));
        }
        else
        {
            heading!.Arrange(new Rect(0, 0, finalSize.Width, heading.DesiredSize.Height));
            aside.Arrange(new Rect(0, heading.DesiredSize.Height + Gap, Math.Min(aside.DesiredSize.Width, finalSize.Width), aside.DesiredSize.Height));
        }

        return finalSize;
    }

    private (UIElement? Heading, UIElement? Aside) HeadingAndAside() =>
        (InternalChildren.Count > 0 ? InternalChildren[0] : null, InternalChildren.Count > 1 ? InternalChildren[1] : null);
}
