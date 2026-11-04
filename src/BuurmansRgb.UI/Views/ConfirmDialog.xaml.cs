using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// Asks whether something may be deleted or replaced, in the look of the app and with its own Dutch
// buttons "Annuleren" and "Verwijderen" (or another word for what happens, such as "Terugzetten"),
// instead of a MessageBox with the buttons in the language of Windows. Modal on the window that
// asks; Escape, the cross and Alt+F4 cancel. A screen reader reads the question as the name of the
// window, and the detail as its help.
public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string question, string? detail = null, string? confirmText = null)
    {
        InitializeComponent();
        ConfirmButton.Content = confirmText ?? Strings.ConfirmDelete;
        Title = question;
        QuestionText.Text = question;
        DetailText.Text = detail ?? string.Empty;
        DetailText.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetHelpText(this, detail ?? string.Empty);
    }

    // Whether the user chose "Verwijderen" (or the confirmText); false until then.
    public bool IsConfirmed { get; private set; }

    // Shows over its owner, which waits until the user answers; true for "Verwijderen" (or the
    // confirmText).
    public bool Ask()
    {
        ShowDialog();
        return IsConfirmed;
    }

    // The question before a scene is deleted, with its name.
    internal static ConfirmDialog ForScene(Window owner, string name) =>
        For(owner, UiText.Format(Strings.ProfileDeleteConfirm, name));

    // The question before an own effect is deleted, with its name, and what then happens to the
    // devices and scenes that have it.
    internal static ConfirmDialog ForCustomEffect(Window owner, string name) =>
        For(owner, UiText.Format(Strings.CustomEffectDeleteConfirm, name), Strings.CustomEffectDeleteConfirmDetail);

    // The question before a backup replaces the settings, scenes and own effects, with the name of
    // the file, and "Terugzetten" as the answer.
    internal static ConfirmDialog ForRestore(Window owner, string fileName) =>
        For(owner, Strings.BackupRestoreConfirm, UiText.Format(Strings.BackupRestoreConfirmDetail, fileName), Strings.BackupRestoreConfirmButton);

    // The dialog that belongs to owner, before it shows.
    internal static ConfirmDialog For(Window owner, string question, string? detail = null, string? confirmText = null) =>
        new(question, detail, confirmText) { Owner = owner };

    // SizeToContent measures the window with the frame of Windows, which the WindowChrome takes
    // away; WPF does not measure again on its own, which left empty space on the right and at
    // the bottom. Measured again, the window is its content plus its border. WPF already centered
    // it on its owner with the size of before, so it shrinks around its center and stays in the
    // middle of the owner; a window that is put somewhere itself stays there.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var before = RenderSize;
        InvalidateMeasure();
        UpdateLayout();
        if (WindowStartupLocation == WindowStartupLocation.Manual || before.Width == 0 || before.Height == 0)
        {
            return;
        }

        Left -= (RenderSize.Width - before.Width) / 2;
        Top -= (RenderSize.Height - before.Height) / 2;
    }

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
