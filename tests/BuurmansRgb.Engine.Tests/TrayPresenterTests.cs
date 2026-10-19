using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.SettingsApp;
using BuurmansRgb.Engine.Tray;

namespace BuurmansRgb.Engine.Tests;

public sealed class TrayPresenterTests
{
    private readonly FakeEngineHost _host = new();
    private readonly FakeAutoStart _autoStart = new();
    private readonly FakeSettingsAppLauncher _launcher = new();
    private readonly FakeTrayView _view = new();

    [Fact]
    public async Task StartAsync_StartsHostWithoutNotification()
    {
        await CreatePresenter().StartAsync(TestContext.Current.CancellationToken);

        Assert.True(_host.Started);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public async Task StartAsync_HostFails_ShowsDutchErrorWithoutExceptionMessage()
    {
        _host.StartException = new IOException("Disk unavailable.");

        await CreatePresenter().StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([(Strings.StartupFailed, TrayNotificationKind.Error)], _view.Notifications);
    }

    [Fact]
    public async Task StartAsync_CancelledOnShutdown_ShowsNothing()
    {
        var shutdown = new CancellationToken(canceled: true);
        _host.StartException = new OperationCanceledException(shutdown);

        await CreatePresenter().StartAsync(shutdown);

        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public async Task StartAsync_CancelledWithoutShutdown_ShowsError()
    {
        _host.StartException = new TaskCanceledException("Discovery timed out.");

        await CreatePresenter().StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal([(Strings.StartupFailed, TrayNotificationKind.Error)], _view.Notifications);
    }

    [Fact]
    public void OpenSettings_Launched_ShowsNothing()
    {
        CreatePresenter().OpenSettings();

        Assert.Equal(1, _launcher.LaunchCount);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public void OpenSettings_AppMissing_ShowsInfo()
    {
        _launcher.Result = SettingsAppLaunchResult.NotFound;

        CreatePresenter().OpenSettings();

        Assert.Equal([(Strings.SettingsAppNotFound, TrayNotificationKind.Info)], _view.Notifications);
    }

    [Fact]
    public void OpenSettings_LaunchFails_ShowsWarning()
    {
        _launcher.Result = SettingsAppLaunchResult.Failed;

        CreatePresenter().OpenSettings();

        Assert.Equal([(Strings.SettingsAppLaunchFailed, TrayNotificationKind.Warning)], _view.Notifications);
    }

    [Fact]
    public void SetPaused_PausesHostAndSwitchesToolTip()
    {
        var presenter = CreatePresenter();

        presenter.SetPaused(true);
        Assert.True(_host.Paused);
        Assert.Equal(Strings.TrayToolTipPaused, _view.ToolTip);

        presenter.SetPaused(false);
        Assert.False(_host.Paused);
        Assert.Equal(Strings.AppName, _view.ToolTip);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RefreshAutoStart_ShowsCurrentState(bool enabled)
    {
        _autoStart.IsEnabled = enabled;
        _view.AutoStartChecked = !enabled;

        CreatePresenter().RefreshAutoStart();

        Assert.Equal(enabled, _view.AutoStartChecked);
    }

    [Fact]
    public void SetAutoStart_Succeeds_EnablesWithoutNotification()
    {
        CreatePresenter().SetAutoStart(true);

        Assert.True(_autoStart.IsEnabled);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public void SetAutoStart_Fails_RevertsCheckmarkAndWarns()
    {
        _autoStart.SetEnabledException = new UnauthorizedAccessException();
        _view.AutoStartChecked = true;

        CreatePresenter().SetAutoStart(true);

        Assert.False(_view.AutoStartChecked);
        Assert.Equal([(Strings.AutoStartFailed, TrayNotificationKind.Warning)], _view.Notifications);
    }

    private TrayPresenter CreatePresenter() => new(_host, _autoStart, _launcher, _view);

    private sealed class FakeEngineHost : IEngineHost
    {
        public Exception? StartException { get; set; }

        public bool Started { get; private set; }

        public bool Paused { get; private set; }

        public Task StartAsync(CancellationToken ct)
        {
            if (StartException is not null)
            {
                return Task.FromException(StartException);
            }

            Started = true;
            return Task.CompletedTask;
        }

        public void SetPaused(bool paused) => Paused = paused;
    }

    private sealed class FakeAutoStart : IAutoStart
    {
        public Exception? SetEnabledException { get; set; }

        public bool IsEnabled { get; set; }

        public void SetEnabled(bool enabled)
        {
            if (SetEnabledException is not null)
            {
                throw SetEnabledException;
            }

            IsEnabled = enabled;
        }
    }

    private sealed class FakeSettingsAppLauncher : ISettingsAppLauncher
    {
        public SettingsAppLaunchResult Result { get; set; } = SettingsAppLaunchResult.Launched;

        public int LaunchCount { get; private set; }

        public SettingsAppLaunchResult Launch()
        {
            LaunchCount++;
            return Result;
        }
    }

    private sealed class FakeTrayView : ITrayView
    {
        public string? ToolTip { get; private set; }

        public bool AutoStartChecked { get; set; }

        public List<(string Text, TrayNotificationKind Kind)> Notifications { get; } = [];

        public void SetToolTip(string text) => ToolTip = text;

        public void SetAutoStartChecked(bool isChecked) => AutoStartChecked = isChecked;

        public void ShowNotification(string text, TrayNotificationKind kind) => Notifications.Add((text, kind));
    }
}
