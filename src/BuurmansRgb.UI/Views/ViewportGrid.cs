using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BuurmansRgb.UI.Views;

// A Grid to put in a ScrollViewer: it takes the height of the visible area, so its star rows fill
// it, but never less than its rows need at least (the Auto rows and the MinHeight of the star
// rows), so a window that is too low scrolls instead of cutting off what does not fit. In a plain
// ScrollViewer a Grid gets endless height and its lists would grow to all their items.
public sealed class ViewportGrid : Grid
{
    private ScrollViewer? _viewer;

    protected override Size MeasureOverride(Size constraint)
    {
        var viewer = FindViewer();
        if (viewer is null || viewer.ViewportHeight <= 0)
        {
            return base.MeasureOverride(constraint);
        }

        // The margin lies outside what this measures.
        var height = Math.Max(0, viewer.ViewportHeight - Margin.Top - Margin.Bottom);
        var needed = base.MeasureOverride(new Size(constraint.Width, height));
        return new Size(needed.Width, Math.Max(needed.Height, height));
    }

    private ScrollViewer? FindViewer()
    {
        if (_viewer is not null)
        {
            return _viewer;
        }

        for (var parent = VisualTreeHelper.GetParent(this); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer viewer)
            {
                _viewer = viewer;
                _viewer.ScrollChanged += (_, e) =>
                {
                    if (e.ViewportHeightChange != 0)
                    {
                        InvalidateMeasure();
                    }
                };
                return _viewer;
            }
        }

        return null;
    }
}
