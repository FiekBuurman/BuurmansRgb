using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BuurmansRgb.UI.Resources;
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

    // The list is read when it opens, so a game started a moment ago is in it.
    private void OnRunningProgramsOpened(object? sender, EventArgs e) => Automation?.FindProgramsCommand.Execute(null);

    // The pick goes into the program field; the list goes back to its hint, so the same program can
    // be picked again after the field changed. Posted: WPF ignores a change while it selects.
    private void OnRunningProgramPicked(object sender, SelectionChangedEventArgs e)
    {
        if (RunningPrograms.SelectedItem is string program)
        {
            Automation?.PickProgramCommand.Execute(program);
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
