using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.SettingsApp;

namespace BuurmansRgb.Engine.Tray;

internal sealed class TrayApplicationContext : ApplicationContext, ITrayView
{
    private const int BalloonTimeoutMs = 5000;

    private readonly TrayPresenter _presenter;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly SynchronizationContext _uiContext;

    public TrayApplicationContext(IEngineHost host, IAutoStart autoStart, ISettingsAppLauncher settingsAppLauncher)
    {
        _presenter = new TrayPresenter(host, autoStart, settingsAppLauncher, this);

        var pauseItem = new ToolStripMenuItem(Strings.MenuPauseEffects) { CheckOnClick = true };
        pauseItem.CheckedChanged += (_, _) => _presenter.SetPaused(pauseItem.Checked);
        _autoStartItem = new ToolStripMenuItem(Strings.MenuAutoStart) { CheckOnClick = true };
        _autoStartItem.Click += (_, _) => _presenter.SetAutoStart(_autoStartItem.Checked);

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange(
        [
            new ToolStripMenuItem(Strings.MenuOpenSettings, image: null, (_, _) => _presenter.OpenSettings()),
            pauseItem,
            _autoStartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem(Strings.MenuExit, image: null, (_, _) => ExitThread()),
        ]);
        _menu.Opening += (_, _) => _presenter.RefreshAutoStart();

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
                _presenter.OpenSettings();
            }
        };

        // Creating the menu control installed the WinForms context for this thread.
        _uiContext = SynchronizationContext.Current ?? throw new InvalidOperationException("No UI synchronization context.");
    }

    public void RequestOpenSettings() => _uiContext.Post(_ => _presenter.OpenSettings(), state: null);

    public Task StartAsync(CancellationToken ct) => _presenter.StartAsync(ct);

    void ITrayView.SetToolTip(string text) => _notifyIcon.Text = text;

    void ITrayView.SetAutoStartChecked(bool isChecked) => _autoStartItem.Checked = isChecked;

    void ITrayView.ShowNotification(string text, TrayNotificationKind kind) =>
        _notifyIcon.ShowBalloonTip(BalloonTimeoutMs, Strings.AppName, text, ToToolTipIcon(kind));

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

    private static ToolTipIcon ToToolTipIcon(TrayNotificationKind kind) => kind switch
    {
        TrayNotificationKind.Warning => ToolTipIcon.Warning,
        TrayNotificationKind.Error => ToolTipIcon.Error,
        _ => ToolTipIcon.Info,
    };
}
