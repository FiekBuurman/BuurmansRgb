using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views.Pages;

public partial class ColorPage : UserControl
{
    public ColorPage() => InitializeComponent();

    // "+" turns itself off once the color is in the list, which would leave the keyboard focus
    // nowhere: it moves on to the new tile. Click comes before the command adds the color.
    private void OnAddCustomColorClick(object sender, RoutedEventArgs e)
    {
        var count = CustomColorTiles.Items.Count;

        // Once the tiles are laid out again, so the new one exists.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var added = CustomColorTiles.Items.Count;
            if (added > count && CustomColorTiles.ItemContainerGenerator.ContainerFromIndex(added - 1) is { } next && FirstButton(next) is { } tile)
            {
                tile.Focus();
            }
        });
    }

    // Delete on an own color of the toolbox takes it away.
    private void OnCustomColorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && sender is Button tile)
        {
            e.Handled = true;
            RemoveCustomColor(tile);
        }
    }

    // "Weghalen" in the context menu of an own color.
    private void OnRemoveCustomColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu { PlacementTarget: Button tile } })
        {
            RemoveCustomColor(tile);
        }
    }

    // The tile goes with its color, and the keyboard focus moves on to the tile that takes its
    // place, the one before it when it was the last, or "+" when none is left; else it would be
    // nowhere.
    private void RemoveCustomColor(Button tile)
    {
        if (tile.DataContext is not CustomColorItem item)
        {
            return;
        }

        var index = CustomColorTiles.Items.IndexOf(item);
        item.RemoveCommand.Execute(null);

        // Once the tiles are laid out again, so the one in its place exists.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var count = CustomColorTiles.Items.Count;
            var next = count == 0 ? null : CustomColorTiles.ItemContainerGenerator.ContainerFromIndex(Math.Min(index, count - 1));
            if (next is not null && FirstButton(next) is { } nextTile)
            {
                nextTile.Focus();
            }
            else
            {
                AddCustomColor.Focus();
            }
        });
    }

    private static Button? FirstButton(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if ((child as Button ?? FirstButton(child)) is { } button)
            {
                return button;
            }
        }

        return null;
    }
}
