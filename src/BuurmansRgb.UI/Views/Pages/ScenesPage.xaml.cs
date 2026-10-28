using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Win32;

namespace BuurmansRgb.UI.Views.Pages;

public partial class ScenesPage : UserControl
{
    public ScenesPage() => InitializeComponent();

    private ProfileAutomationViewModel? Automation => (DataContext as MainViewModel)?.Profiles.Automation;

    // The name field shows instead of the button; the focus goes with it, so typing can start at once.
    private void OnNewSceneClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => NewSceneName.Focus());

    // The new time is the last row; the focus goes to its field, so typing can start at once.
    private void OnAddTimeClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (TimesList.ItemContainerGenerator.ContainerFromIndex(TimesList.Items.Count - 1) is DependencyObject row && VisualTreeSearch.FirstDescendant<TextBox>(row) is { } field)
            {
                field.Focus();
            }
        });

    // A removed row takes its button with it; the focus goes to "+ Nog een tijd" instead of nowhere.
    private void OnRemoveTimeClick(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!((UIElement)sender).IsVisible)
            {
                AddTime.Focus();
            }
        });

    // The list is read when it opens, so a game started a moment ago is in it.
    private void OnRunningProgramsOpened(object? sender, EventArgs e) => Automation?.FindProgramsCommand.Execute(null);

    // The exe name of the pick goes into the program field; the list goes back to its hint, so the
    // same program can be picked again after the field changed. Posted: WPF ignores a change while
    // it selects.
    private void OnRunningProgramPicked(object sender, SelectionChangedEventArgs e)
    {
        if (RunningPrograms.SelectedItem is RunningProgram program)
        {
            Automation?.PickRunningProgramCommand.Execute(program);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => RunningPrograms.SelectedItem = null);
        }
    }

    private void OnBrowseProgramClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = Strings.ProgramBrowseTitle, Filter = Strings.ProgramBrowseFilter, CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            Automation?.PickProgramCommand.Execute(dialog.FileName);
        }
    }
}
