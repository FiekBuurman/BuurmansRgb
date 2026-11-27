using System.Windows;
using System.Windows.Controls;

namespace BuurmansRgb.UI.Views;

// Lays cards out in equal columns, as many as fit at MinColumnWidth up to MaxColumns (§ 6: two to
// three lamps, three to four scenes side by side), with Gap between them. A row is as high as its
// highest card. A collapsed child takes no place, so it leaves no empty row and no gap behind.
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
        var cards = CardCount();
        var height = 0.0;
        var rowHeight = 0.0;
        var place = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            if (place % columns == columns - 1 || place == cards - 1)
            {
                height += rowHeight + (place >= columns ? Gap : 0);
                rowHeight = 0;
            }

            place++;
        }

        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : columnWidth * columns + Gap * (columns - 1);
        return new Size(cards == 0 ? 0 : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width, MinColumnWidth, Gap, MaxColumns);
        var columnWidth = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns);
        var y = 0.0;
        var next = 0;
        while (next < InternalChildren.Count)
        {
            // A row is the next columns cards, with the collapsed children between them.
            var first = next;
            var rowHeight = 0.0;
            for (var placed = 0; next < InternalChildren.Count && placed < columns; next++)
            {
                var child = InternalChildren[next];
                if (child.Visibility != Visibility.Collapsed)
                {
                    rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                    placed++;
                }
            }

            var column = 0;
            for (var i = first; i < next; i++)
            {
                var child = InternalChildren[i];
                if (child.Visibility == Visibility.Collapsed)
                {
                    child.Arrange(default);
                    continue;
                }

                child.Arrange(new Rect(column++ * (columnWidth + Gap), y, columnWidth, rowHeight));
            }

            y += rowHeight + Gap;
        }

        return finalSize;
    }

    // The children that take a place: a collapsed one takes none.
    private int CardCount()
    {
        var count = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility != Visibility.Collapsed)
            {
                count++;
            }
        }

        return count;
    }
}
