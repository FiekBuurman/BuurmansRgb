using System.Windows;
using System.Windows.Media;

namespace BuurmansRgb.UI.Views;

// Finds an element in the visual tree of a template, such as the button of a tile or the field
// of a row, to give it the keyboard focus.
internal static class VisualTreeSearch
{
    // Depth first: the first one in the order the tree draws them.
    internal static T? FirstDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if ((child as T ?? FirstDescendant<T>(child)) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
