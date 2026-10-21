using System.Diagnostics;
using System.Windows;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI;

// Closing the window ends the process, so the settings app holds no memory while it is closed.
public partial class App : Application
{
    private MainViewModel? _viewModel;
    private FileLog? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _log = FileLog.Start("ui");
        // Logged only; the app still ends as it did, rather than going on in a state nobody planned for.
        DispatcherUnhandledException += (_, args) => Trace.TraceError($"Unhandled exception: {args.Exception}");

        _viewModel = new MainViewModel(PipeEngineConnector.ForEngine());
        MainWindow = new MainWindow { DataContext = _viewModel };
        MainWindow.Show();
        _viewModel.ConnectCommand.Execute(null);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Closes the pipe right away instead of leaving the engine to notice a dead process.
        _viewModel?.Dispose();
        _log?.Dispose();
        base.OnExit(e);
    }
}
