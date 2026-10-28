using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

// Asks whether something may be deleted, in the look of the app and with its own Dutch buttons
// "Annuleren" and "Verwijderen", instead of a MessageBox with the buttons in the language of
// Windows. Modal on the window that asks; Escape, the cross and Alt+F4 cancel. A screen reader
// reads the question as the name of the window, and the detail as its help.
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string question, string? detail = null)
    {
        InitializeComponent();
        Title = question;
        QuestionText.Text = question;
        DetailText.Text = detail ?? string.Empty;
        DetailText.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetHelpText(this, detail ?? string.Empty);
    }

    // Whether the user chose "Verwijderen"; false until then.
    public bool IsConfirmed { get; private set; }

    // Asks over owner, which waits until the user answers.
    public static bool Ask(Window owner, string question, string? detail = null)
    {
        var dialog = For(owner, question, detail);
        dialog.ShowDialog();
        return dialog.IsConfirmed;
    }

    // The dialog that belongs to owner, before it shows.
    internal static ConfirmDialog For(Window owner, string question, string? detail = null) =>
        new(question, detail) { Owner = owner };

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
