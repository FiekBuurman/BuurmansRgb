using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.Restart;
using BuurmansRgb.Engine.SettingsApp;

namespace BuurmansRgb.Engine.Tray;

internal sealed class TrayApplicationContext : ApplicationContext, ITrayView
{
    private const int BalloonTimeoutMs = 5000;

    private readonly TrayPresenter _presenter;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _profilesItem;
    private readonly ToolStripMenuItem _allOnItem;
    private readonly ToolStripMenuItem _allOffItem;
    private readonly SynchronizationContext _uiContext;
    private readonly IEngineHost _host;
    private readonly IServerStatusSource _openRgb;
    private readonly AllOnOffHotkeys _hotkeys;
    private readonly WindowsGlobalHotkeys _globalHotkeys = new();
    private readonly CancellationTokenSource _shutdown = new();

    public TrayApplicationContext(
        IEngineHost host,
        IAutoStart autoStart,
        ISettingsAppLauncher settingsAppLauncher,
        ProfileLibrary profiles,
        LightingController lighting,
        IServerStatusSource openRgb,
        AllOnOffHotkeys hotkeys,
        string logPath,
        Action? trimMemory = null,
        EngineRestarter? restarter = null,
        ServicesRestartReport? restartedWith = null,
        bool openSettingsAfterStart = false)
    {
        _presenter = new TrayPresenter(
            host,
            autoStart,
            settingsAppLauncher,
            profiles,
            lighting,
            hotkeys,
            _globalHotkeys,
            () => Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true })?.Dispose(),
            this,
            restarter,
            restartedWith,
            openSettingsAfterStart);
        _profilesItem = new ToolStripMenuItem(Strings.MenuLoadProfile);
        _allOnItem = new ToolStripMenuItem(Strings.MenuAllOn, image: null, (_, _) => _presenter.SetAllOn(true));
        _allOffItem = new ToolStripMenuItem(Strings.MenuAllOff, image: null, (_, _) => _presenter.SetAllOn(false));

        // Click, not CheckedChanged: the check also follows a pause that ends elsewhere (ShowPause).
        _pauseItem = new ToolStripMenuItem(Strings.MenuPauseEffects) { CheckOnClick = true };
        _pauseItem.Click += (_, _) => _presenter.SetPaused(_pauseItem.Checked);
        _autoStartItem = new ToolStripMenuItem(Strings.MenuAutoStart) { CheckOnClick = true };
        _autoStartItem.Click += (_, _) => _presenter.SetAutoStart(_autoStartItem.Checked);

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange(
        [
            new ToolStripMenuItem(Strings.MenuOpenSettings, image: null, (_, _) => _presenter.OpenSettings()),
            _profilesItem,
            _allOnItem,
            _allOffItem,
            _pauseItem,
            _autoStartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem(Strings.MenuOpenLog, image: null, (_, _) => _presenter.OpenLog()),
            new ToolStripMenuItem(Strings.MenuRestart, image: null, async (_, _) => await RestartAsync()),
            new ToolStripMenuItem(Strings.MenuExit, image: null, (_, _) => ExitThread()),
        ]);
        _menu.Opening += (_, _) =>
        {
            _presenter.RefreshAutoStart();
            _presenter.RefreshProfiles();
            _presenter.RefreshAllOnOff();
        };

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

        // Drawing the menu loads and touches memory the engine does not need once it closed, as
        // with the settings app. Posted, so a clicked item does its work first.
        if (trimMemory is not null)
        {
            _menu.Closed += (_, _) => _uiContext.Post(_ => trimMemory(), state: null);
        }

        // Listening first, so no change gets lost between reading the status and listening.
        _openRgb = openRgb;
        _openRgb.StatusChanged += OnOpenRgbStatusChanged;
        _presenter.ShowOpenRgbStatus(_openRgb.Status);
        _host = host;
        _host.PauseChanged += OnPauseChanged;
        _presenter.ShowPause(_host.Pause);
        _hotkeys = hotkeys;
        _hotkeys.EnabledChanged += OnHotkeysEnabledChanged;

        // Continues on the UI thread once Application.Run pumps messages; "Afsluiten" cancels it.
        _ = _presenter.StartAsync(_shutdown.Token);
    }

    public void RequestOpenSettings() => _uiContext.Post(_ => _presenter.OpenSettings(), state: null);

    // From another thread (--exit): the same as "Afsluiten" in the menu.
    public void RequestExit() => _uiContext.Post(_ => ExitThread(), state: null);

    void ITrayView.SetToolTip(string text) => _notifyIcon.Text = text;

    // "Herstarten": exits like "Afsluiten" once the new engine started, which waits for this one.
    // The presenter never throws, so the click handler cannot take the engine down.
    private async Task RestartAsync()
    {
        if (await _presenter.RestartAsync(_shutdown.Token))
        {
            ExitThread();
        }
    }

    void ITrayView.SetAutoStartChecked(bool isChecked) => _autoStartItem.Checked = isChecked;

    void ITrayView.SetPauseChecked(bool isChecked) => _pauseItem.Checked = isChecked;

    // Without profiles the submenu explains where to make them; when they cannot be loaded now,
    // the whole item is greyed out (an unreadable file also gets a notification).
    void ITrayView.ShowProfiles(IReadOnlyList<string>? names, string? active)
    {
        var oldItems = _profilesItem.DropDownItems.Cast<ToolStripItem>().ToList();
        _profilesItem.DropDownItems.Clear();
        oldItems.ForEach(item => item.Dispose());

        _profilesItem.Enabled = names is not null;
        if (names is null or [])
        {
            _profilesItem.DropDownItems.Add(new ToolStripMenuItem(Strings.MenuNoProfiles) { Enabled = false });
            return;
        }

        foreach (var name in names)
        {
            // A single & would underline the next letter instead of showing itself.
            _profilesItem.DropDownItems.Add(new ToolStripMenuItem(name.Replace("&", "&&", StringComparison.Ordinal), image: null, (_, _) => _presenter.LoadProfile(name))
            {
                Checked = active is not null && ProfileNames.Comparer.Equals(name, active),
            });
        }
    }

    void ITrayView.ShowAllOnOff(bool canSwitchOn, bool canSwitchOff)
    {
        _allOnItem.Enabled = canSwitchOn;
        _allOffItem.Enabled = canSwitchOff;
    }

    // Only shown: the keys work through RegisterHotKey, also while the menu is closed.
    void ITrayView.ShowAllOnOffHotkeys(string? allOn, string? allOff)
    {
        _allOnItem.ShortcutKeyDisplayString = allOn;
        _allOffItem.ShortcutKeyDisplayString = allOff;
    }

    void ITrayView.ShowNotification(string text, TrayNotificationKind kind) =>
        _notifyIcon.ShowBalloonTip(BalloonTimeoutMs, Strings.AppName, text, ToToolTipIcon(kind));

    protected override void ExitThreadCore()
    {
        _shutdown.Cancel();
        _notifyIcon.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _openRgb.StatusChanged -= OnOpenRgbStatusChanged;
            _host.PauseChanged -= OnPauseChanged;
            _hotkeys.EnabledChanged -= OnHotkeysEnabledChanged;
            _globalHotkeys.Dispose();
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _menu.Dispose();
            _shutdown.Dispose();
        }

        base.Dispose(disposing);
    }

    // The status changes on the provider's thread; the tray icon belongs to this one.
    private void OnOpenRgbStatusChanged(object? sender, ServerStatus status) =>
        _uiContext.Post(_ => _presenter.ShowOpenRgbStatus(status), state: null);

    // Also raised on an IPC thread, when the settings app ends the pause.
    private void OnPauseChanged(object? sender, PauseReasons pause) =>
        _uiContext.Post(_ => _presenter.ShowPause(pause), state: null);

    // Also from an IPC thread, when the settings app turns the hotkeys on or off; the keys belong
    // to the window on this thread.
    private void OnHotkeysEnabledChanged(object? sender, EventArgs e) =>
        _uiContext.Post(_ => _presenter.ApplyHotkeys(), state: null);

    private static ToolTipIcon ToToolTipIcon(TrayNotificationKind kind) => kind switch
    {
        TrayNotificationKind.Warning => ToolTipIcon.Warning,
        TrayNotificationKind.Error => ToolTipIcon.Error,
        _ => ToolTipIcon.Info,
    };
}
