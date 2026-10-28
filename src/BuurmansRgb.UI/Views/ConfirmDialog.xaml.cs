using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

// Asks whether something may be deleted, in the look of the app and with its own Dutch buttons
// "Annuleren" and "Verwijderen", instead of a MessageBox with the buttons in the language of
// Windows. Modal on the window that asks; Escape, the cross and Alt+F4 cancel. A screen reader
// reads the question as the name of the window, and the detail as its help.
public partial class ConfirmDialog : Window
{
    private bool _isRendered;

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

    // Shows over its owner, which waits until the user answers; true for "Verwijderen".
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

    // The dialog that belongs to owner, before it shows.
    internal static ConfirmDialog For(Window owner, string question, string? detail = null) =>
        new(question, detail) { Owner = owner };

    // SizeToContent measures the window with the frame of Windows, which the WindowChrome takes
    // away; WPF does not measure again on its own, which left empty space on the right and at
    // the bottom. Measured again, the window is its content plus its border.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        InvalidateMeasure();
    }

    // WPF centered the window on its owner with the size of before; until it shows, it shrinks
    // around its center, so it stays in the middle of the owner.
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        var previous = sizeInfo.PreviousSize;
        if (_isRendered || previous.Width == 0 || previous.Height == 0)
        {
            return;
        }

        Left -= (sizeInfo.NewSize.Width - previous.Width) / 2;
        Top -= (sizeInfo.NewSize.Height - previous.Height) / 2;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        _isRendered = true;
        base.OnContentRendered(e);
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
