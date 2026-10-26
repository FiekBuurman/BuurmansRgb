using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

    // Enter keeps the name as Tab does, so the box need not lose the focus first.
    private void OnCustomEffectNameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
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

    // Once the tiles are laid out again, so the one at the selected place exists.
    private void FocusSelectedStep(bool now = false)
    {
        void Focus()
        {
            if (DataContext is MainViewModel { CustomEffectEditor: var editor }
                && StepTiles.ItemContainerGenerator.ContainerFromIndex(editor.SelectedIndex) is { } container
                && FirstButton(container) is { } tile)
            {
                tile.Focus();
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
