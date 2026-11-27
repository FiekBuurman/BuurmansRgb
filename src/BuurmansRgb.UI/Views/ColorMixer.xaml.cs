using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// A panel rather than a UserControl, which would only build its parts once it shows: so its elements
// are there for UI Automation and the tests even while it is hidden, as they were on the page, and it
// adds no element of its own, so a screen reader meets the same tree as before this control. A
// StackPanel around the CardGrid, which is sealed, and lays it out as the page did.
public partial class ColorMixer : StackPanel
{
    public ColorMixer() => InitializeComponent();

    // "+" turns itself off once the color is in the list, which would leave the keyboard focus
    // nowhere: it moves on to the new tile. Click comes before the command adds the color.
    private void OnAddCustomColorClick(object sender, RoutedEventArgs e)
    {
        var count = CustomColorTiles.Items.Count;

        // Once the tiles are laid out again, so the new one exists; only if the color was added.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var added = CustomColorTiles.Items.Count;
            if (added > count)
            {
                CustomColorTiles.FocusTileAt(added - 1, fallback: null, now: true);
            }
        });
    }

    // A hex code that is no color makes way for the code of the color once the field loses the focus.
    private void OnHexLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ColorPicker.EndHexEdit();
        }
    }

    // While the hex field has the keyboard focus, a color shown leaves a code that is no color yet.
    private void OnHexKeyboardFocusChanged(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ColorPicker.IsHexFocused = e.RoutedEvent == Keyboard.GotKeyboardFocusEvent;
        }
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
        CustomColorTiles.FocusTileAt(index, fallback: AddCustomColor);
    }
}
