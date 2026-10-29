using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

        // Once the tiles are laid out again, so the new one exists; only if the color was added.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var added = CustomColorTiles.Items.Count;
            if (added > count)
            {
                FocusTile(CustomColorTiles, added - 1, fallback: null, now: true);
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
        FocusTile(CustomColorTiles, index, fallback: AddCustomColor);
    }

    // Enter keeps the name as Tab does, so the box need not lose the focus first; Escape puts back
    // the name the effect has, and the box keeps the focus.
    private void OnCustomEffectNameKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && box.DataContext is CustomEffectEditorViewModel editor)
        {
            editor.CancelNameEdit();
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            e.Handled = true;
        }
    }

    // The binding writes only here and with Enter, so a name that is not kept gives one message, the
    // one that says it made way for the name of the effect, which the box then shows again.
    private void OnCustomEffectNameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: CustomEffectEditorViewModel editor } box)
        {
            editor.EndNameEdit(box.Text);
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }
    }

    // Delete on a step takes it away, as "−" does; the focus goes to the step that takes its place.
    private void OnStepKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || DataContext is not MainViewModel { CustomEffectEditor: var editor } || sender is not Button { DataContext: CustomEffectStepItem step })
        {
            return;
        }

        e.Handled = true;
        editor.SelectStepCommand.Execute(step.Index);
        if (editor.RemoveStepCommand.CanExecute(null))
        {
            editor.RemoveStepCommand.Execute(null);
            FocusSelectedStep();
        }
    }

    // "−" turns itself off at the last step, and "←" and "→" at the ends, which would leave the
    // keyboard focus nowhere: it goes to the selected step then. Click comes before the command.
    private void OnStepButtonClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!button.IsEnabled || !button.IsKeyboardFocused)
            {
                FocusSelectedStep(now: true);
            }
        });
    }

    private void FocusSelectedStep(bool now = false)
    {
        if (DataContext is MainViewModel { CustomEffectEditor: var editor })
        {
            FocusTile(StepTiles, editor.SelectedIndex, fallback: null, now);
        }
    }

    // Focuses the tile at index, or the last one when the list got shorter, and without tiles the
    // fallback. Unless now, once the tiles are laid out again, so the one at that place exists.
    private void FocusTile(ItemsControl tiles, int index, UIElement? fallback, bool now = false)
    {
        void Focus()
        {
            var count = tiles.Items.Count;
            if (count > 0
                && tiles.ItemContainerGenerator.ContainerFromIndex(Math.Clamp(index, 0, count - 1)) is { } container
                && VisualTreeSearch.FirstDescendant<Button>(container) is { } tile)
            {
                tile.Focus();
            }
            else
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
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Focus);
        }
    }

    // Without a direction the slider stands at 0, where Home or a click on its knob changes no
    // value; they choose 0° instead, as moving the slider chooses its degrees.
    private void OnDirectionKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.None)
        {
            ChooseDirection(sender);
        }
    }

    private void OnDirectionThumbDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!e.Canceled)
        {
            ChooseDirection(sender);
        }
    }

    private static void ChooseDirection(object slider)
    {
        if (slider is FrameworkElement { DataContext: MainViewModel viewModel } && viewModel.ChooseDirectionCommand.CanExecute(null))
        {
            viewModel.ChooseDirectionCommand.Execute(null);
        }
    }
}
