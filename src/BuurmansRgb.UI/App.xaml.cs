using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI;

// Closing the window ends the process, so the settings app holds no memory while it is closed.
// One settings app per Windows session: a second start ("Instellingen openen" again, a double
// click on the tray icon) brings the open window to the front and stops. "Herstarten" in the tray
// asks it to close, as --exit does with the engine, and the new engine opens it again.
public partial class App : Application
{
    private const string InstanceName = EngineRpc.SettingsAppName;

    // ASFW_ANY: whichever process the activation reaches may take the foreground from this one.
    private const int AnyProcess = -1;

    private SingleInstance? _instance;
    private IDisposable? _activation;
    private IDisposable? _exit;
    private MainViewModel? _viewModel;
    private FileLog? _log;
    private SystemHighContrast? _highContrast;
    private ContrastTheme? _contrastTheme;
    private SystemTextScale? _systemTextScale;
    private TextScale? _textScale;
    private SystemAnimationEffects? _animationEffects;

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

        // After the single-instance check, so a second start does not write to the log of the first.
        _log = FileLog.Start(FileLog.UiName);
        // Logged only; the app still ends as it did, rather than going on in a state nobody planned for.
        DispatcherUnhandledException += (_, args) => Trace.TraceError($"Unhandled exception: {args.Exception}");

        // Before the window loads, so it opens in the colors of a contrast theme that is on.
        _highContrast = new SystemHighContrast();
        _contrastTheme = ContrastTheme.Follow(Resources, _highContrast);
        // Likewise, so it opens in the text size of Windows.
        _systemTextScale = new SystemTextScale();
        _textScale = TextScale.Follow(Resources, _systemTextScale);
        // The preview stands still while "Animatie-effecten" of Windows is off.
        _animationEffects = new SystemAnimationEffects();

        _viewModel = new MainViewModel(PipeEngineConnector.ForEngine(), appInfo: AppInfoViewModel.ForThisApp(ShellLauncher.Open), animationEffects: _animationEffects);
        var window = new MainWindow { DataContext = _viewModel };
        MainWindow = window;
        window.Show();
        _activation = _instance.ListenForActivation(() => Dispatcher.BeginInvoke(window.BringToFront));
        _exit = _instance.ListenForExit(() => Dispatcher.BeginInvoke(() => Shutdown()));
        _viewModel.ConnectCommand.Execute(null);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Before what the activation uses: it waits for a callback that is still running.
        _activation?.Dispose();
        _exit?.Dispose();
        // Closes the pipe right away instead of leaving the engine to notice a dead process.
        _viewModel?.Dispose();
        _contrastTheme?.Dispose();
        _highContrast?.Dispose();
        _textScale?.Dispose();
        _systemTextScale?.Dispose();
        _animationEffects?.Dispose();
        _instance?.Dispose();
        _log?.Dispose();
        base.OnExit(e);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
