using System.Runtime.InteropServices;
using System.Windows;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI;

// Closing the window ends the process, so the settings app holds no memory while it is closed.
// One settings app per Windows session: a second start ("Instellingen openen" again, a double
// click on the tray icon) brings the open window to the front and stops.
public partial class App : Application
{
    private const string InstanceName = "BuurmansRgb.UI";

    // ASFW_ANY: whichever process the activation reaches may take the foreground from this one.
    private const int AnyProcess = -1;

    private SingleInstance? _instance;
    private IDisposable? _activation;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instance = SingleInstance.Acquire(InstanceName);
        if (!_instance.IsPrimary)
        {
            // This start has the foreground (the user just asked for it); without passing it on,
            // Windows would only flash the open window in the taskbar.
            AllowSetForegroundWindow(AnyProcess);
            _instance.SignalPrimary();
            Shutdown();
            return;
        }

        _viewModel = new MainViewModel(PipeEngineConnector.ForEngine());
        var window = new MainWindow { DataContext = _viewModel };
        MainWindow = window;
        window.Show();
        _activation = _instance.ListenForActivation(() => Dispatcher.BeginInvoke(window.BringToFront));
        _viewModel.ConnectCommand.Execute(null);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Before what the activation uses: it waits for a callback that is still running.
        _activation?.Dispose();
        // Closes the pipe right away instead of leaving the engine to notice a dead process.
        _viewModel?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
