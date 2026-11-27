using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// A panel rather than a UserControl, which would only build its parts once it shows: so its elements
// are there for UI Automation and the tests even while it is hidden, as they were on the page, and it
// adds no element of its own, so a screen reader meets the same tree as before this control.
public partial class CustomEffectEditor : StackPanel
{
    public CustomEffectEditor() => InitializeComponent();

    // The binding writes only here and with Enter (CommitOnEnter), so a name that is not kept gives one message, the
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
            StepTiles.FocusTileAt(editor.SelectedIndex, fallback: null, now);
        }
    }
}
