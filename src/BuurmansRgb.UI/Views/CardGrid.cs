using System.Windows;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// Lays cards out in equal columns, as many as fit at MinColumnWidth up to MaxColumns (§ 6: two to
// three lamps, three to four scenes side by side), with Gap between them. A row is as high as its
// highest card.
public sealed class CardGrid : Panel
{
    public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(
        nameof(MinColumnWidth), typeof(double), typeof(CardGrid), new FrameworkPropertyMetadata(260.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(CardGrid), new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(CardGrid), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinColumnWidth
    {
        get => (double)GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    // The number of columns for a width; one when the width is unknown or too small for two.
    internal static int ColumnsFor(double width, double minColumnWidth, double gap, int maxColumns) =>
        double.IsFinite(width) && width > 0
            ? Math.Clamp((int)((width + gap) / (minColumnWidth + gap)), 1, Math.Max(maxColumns, 1))
            : 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = ColumnsFor(availableSize.Width, MinColumnWidth, Gap, MaxColumns);
        var columnWidth = double.IsFinite(availableSize.Width)
            ? Math.Max(0, (availableSize.Width - Gap * (columns - 1)) / columns)
            : MinColumnWidth;
        var height = 0.0;
        var rowHeight = 0.0;
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var child = InternalChildren[i];
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            if (i % columns == columns - 1 || i == InternalChildren.Count - 1)
            {
                height += rowHeight + (i >= columns ? Gap : 0);
                rowHeight = 0;
            }
        }

        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : columnWidth * columns + Gap * (columns - 1);
        return new Size(InternalChildren.Count == 0 ? 0 : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width, MinColumnWidth, Gap, MaxColumns);
        var columnWidth = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        var y = 0.0;
        for (var row = 0; row * columns < InternalChildren.Count; row++)
        {
            var first = row * columns;
            var last = Math.Min(first + columns, InternalChildren.Count);
            var rowHeight = 0.0;
            for (var i = first; i < last; i++)
            {
                rowHeight = Math.Max(rowHeight, InternalChildren[i].DesiredSize.Height);
            }

            for (var i = first; i < last; i++)
            {
                InternalChildren[i].Arrange(new Rect((i - first) * (columnWidth + Gap), y, columnWidth, rowHeight));
            }

            y += rowHeight + Gap;
        }

        return finalSize;
    }
}
