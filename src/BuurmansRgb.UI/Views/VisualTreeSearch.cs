using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Views;

// Finds an element in the visual tree of a template, such as the button of a tile or the field
// of a row, to give it the keyboard focus, or the element around one, such as the scroller of a card.
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

    // The nearest one around element, without element itself.
    internal static T? Ancestor<T>(DependencyObject element) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is T match)
            {
                return match;
            }
        }

        return null;
    }

    // Focuses the first T in the item at index, or in the last item when there are fewer, such as
    // the tile that takes the place of a removed one. False without items, or before the item is
    // laid out, so the caller can give the focus elsewhere.
    internal static bool FocusItemAt<T>(this ItemsControl items, int index) where T : UIElement
    {
        var count = items.Items.Count;
        if (count == 0
            || items.ItemContainerGenerator.ContainerFromIndex(Math.Clamp(index, 0, count - 1)) is not { } container
            || FirstDescendant<T>(container) is not { } element)
        {
            return false;
        }

        element.Focus();
        return true;
    }

    // Focuses the button of the tile at index, or of the last one when the list got shorter, and
    // without tiles the fallback. Unless now, once the tiles are laid out again, so the one at that
    // place exists: after a tile was removed or added.
    internal static void FocusTileAt(this ItemsControl tiles, int index, UIElement? fallback, bool now = false)
    {
        void Focus()
        {
            if (!tiles.FocusItemAt<Button>(index))
            {
                fallback?.Focus();
            }
        }

        if (now)
        {
            Focus();
        }
        else
        {
            tiles.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Focus);
        }
    }
}
