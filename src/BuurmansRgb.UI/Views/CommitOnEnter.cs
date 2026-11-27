using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

// A text box whose text counts with Enter, as when it loses the focus, so the box need not lose
// the focus first; the box keeps it. With a CancelCommand, Escape runs it and the box shows what the
// view model has again, also with the focus kept. IsEnabled gives only Enter, a CancelCommand both.
public static class CommitOnEnter
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(CommitOnEnter), new PropertyMetadata(false, OnChanged));

    public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.RegisterAttached(
        "CancelCommand", typeof(ICommand), typeof(CommitOnEnter), new PropertyMetadata(null, OnChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static ICommand? GetCancelCommand(DependencyObject element) => (ICommand?)element.GetValue(CancelCommandProperty);

    public static void SetCancelCommand(DependencyObject element, ICommand? value) => element.SetValue(CancelCommandProperty, value);

    private static void OnChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox box)
        {
            return;
        }

        box.KeyDown -= OnKeyDown;
        if (GetIsEnabled(box) || GetCancelCommand(box) is not null)
        {
            box.KeyDown += OnKeyDown;
        }
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        if (e.Key == Key.Enter)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && GetCancelCommand(box) is { } cancel)
        {
            if (cancel.CanExecute(null))
            {
                cancel.Execute(null);
            }

            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            e.Handled = true;
        }
    }
}
