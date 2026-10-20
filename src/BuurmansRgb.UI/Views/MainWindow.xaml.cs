using System.Windows;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.Profiles.ConfirmDelete = ConfirmDelete;
            }
        };
    }

    // The button hides once the engine has resumed, which would drop the keyboard focus to the
    // window; the effect is the next thing a user who just resumed may want to change.
    private void OnResumeEffectsClick(object sender, RoutedEventArgs e) => EffectChoice.Focus();

    // A deleted profile cannot come back, so the user confirms it; No is the default button.
    private bool ConfirmDelete(string name) =>
        MessageBox.Show(this, UiText.Format(Strings.ProfileDeleteConfirm, name), Strings.WindowTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
}
