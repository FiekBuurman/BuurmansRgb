using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.SettingsApp;

namespace BuurmansRgb.Engine.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int BalloonTimeoutMs = 5000;

    private readonly EngineHost _host;
    private readonly IAutoStart _autoStart;
    private readonly ISettingsAppLauncher _settingsAppLauncher;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly SynchronizationContext _uiContext;

    public TrayApplicationContext(EngineHost host, IAutoStart autoStart, ISettingsAppLauncher settingsAppLauncher)
    {
        _host = host;
        _autoStart = autoStart;
        _settingsAppLauncher = settingsAppLauncher;

        var pauseItem = new ToolStripMenuItem(Strings.MenuPauseEffects) { CheckOnClick = true };
        pauseItem.CheckedChanged += (_, _) => SetPaused(pauseItem.Checked);
        _autoStartItem = new ToolStripMenuItem(Strings.MenuAutoStart) { CheckOnClick = true };
        _autoStartItem.Click += (_, _) => SetAutoStart(_autoStartItem.Checked);

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange(
        [
            new ToolStripMenuItem(Strings.MenuOpenSettings, image: null, (_, _) => OpenSettings()),
            pauseItem,
            _autoStartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem(Strings.MenuExit, image: null, (_, _) => ExitThread()),
        ]);
        _menu.Opening += (_, _) => _autoStartItem.Checked = _autoStart.IsEnabled;

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIcon.LoadSmall(),
            Text = Strings.AppName,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenSettings();
            }
        };

        // Creating the menu control installed the WinForms context for this thread.
        _uiContext = SynchronizationContext.Current ?? throw new InvalidOperationException("No UI synchronization context.");
    }

    public void RequestOpenSettings() => _uiContext.Post(_ => OpenSettings(), state: null);

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await _host.StartAsync(ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ShowBalloon(string.Format(Strings.StartupFailed, exception.Message), ToolTipIcon.Error);
        }
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OpenSettings()
    {
        if (!_settingsAppLauncher.TryLaunch())
        {
            ShowBalloon(Strings.SettingsAppNotFound, ToolTipIcon.Info);
        }
    }

    private void SetPaused(bool paused)
    {
        _host.SetPaused(paused);
        _notifyIcon.Text = paused ? Strings.TrayToolTipPaused : Strings.AppName;
    }

    private void SetAutoStart(bool enabled)
    {
        try
        {
            _autoStart.SetEnabled(enabled);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _autoStartItem.Checked = _autoStart.IsEnabled;
            ShowBalloon(string.Format(Strings.AutoStartFailed, exception.Message), ToolTipIcon.Warning);
        }
    }

    private void ShowBalloon(string text, ToolTipIcon icon) =>
        _notifyIcon.ShowBalloonTip(BalloonTimeoutMs, Strings.AppName, text, icon);
}
