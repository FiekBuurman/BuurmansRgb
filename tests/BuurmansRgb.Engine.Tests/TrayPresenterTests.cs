using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.SettingsApp;
using BuurmansRgb.Engine.Tray;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class TrayPresenterTests : IDisposable
{
    private readonly FakeEngineHost _host = new();
    private readonly FakeAutoStart _autoStart = new();
    private readonly FakeSettingsAppLauncher _launcher = new();
    private readonly FakeTrayView _view = new();
    private readonly InMemoryProfileStore _profileStore = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly ProfileLibrary _profiles;

    public TrayPresenterTests() => _profiles = new ProfileLibrary(_profileStore, _lighting);

    public void Dispose() => _lighting.Dispose();

    [Fact]
    public async Task StartAsync_StartsHostWithoutNotification()
    {
        await CreatePresenter().StartAsync(Ct);

        Assert.True(_host.Started);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public async Task StartAsync_HostFails_ShowsDutchErrorWithoutExceptionMessage()
    {
        _host.StartException = new IOException("Disk unavailable.");

        await CreatePresenter().StartAsync(Ct);

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

        await CreatePresenter().StartAsync(Ct);

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
        Assert.Equal($"{Strings.TrayToolTipPaused}\nOpenRGB: verbinden…", _view.ToolTip);

        presenter.SetPaused(false);
        Assert.False(_host.Paused);
        Assert.Equal($"{Strings.AppName}\nOpenRGB: verbinden…", _view.ToolTip);
    }

    // The same texts as in OpenRgbStatusViewModelTests, so the tray and the app say the same.
    [Theory]
    [InlineData(ServerState.Connected, 4, "OpenRGB: verbonden, 4 apparaten")]
    [InlineData(ServerState.Connected, 1, "OpenRGB: verbonden, 1 apparaat")]
    [InlineData(ServerState.Connected, 0, "OpenRGB: verbonden, geen apparaten gevonden")]
    [InlineData(ServerState.Unreachable, 0, "OpenRGB: niet bereikbaar")]
    [InlineData(ServerState.Failed, 0, "OpenRGB: geen goed antwoord")]
    [InlineData(ServerState.Connecting, 0, "OpenRGB: verbinden…")]
    public void ShowOpenRgbStatus_PutsItUnderTheNameInTheToolTip(ServerState state, int deviceCount, string text)
    {
        CreatePresenter().ShowOpenRgbStatus(new ServerStatus(state, deviceCount));

        Assert.Equal($"{Strings.AppName}\n{text}", _view.ToolTip);
    }

    [Fact]
    public void ShowOpenRgbStatus_WhilePaused_KeepsThePauseInTheToolTip()
    {
        var presenter = CreatePresenter();
        presenter.SetPaused(true);

        presenter.ShowOpenRgbStatus(new ServerStatus(ServerState.Unreachable));

        Assert.StartsWith(Strings.TrayToolTipPaused + "\n", _view.ToolTip);
    }

    // NotifyIcon refuses a tooltip of more than 127 characters.
    [Fact]
    public void ToolTip_FitsTheTrayIconInEveryState()
    {
        var presenter = CreatePresenter();
        presenter.SetPaused(true);

        foreach (var state in Enum.GetValues<ServerState>())
        {
            presenter.ShowOpenRgbStatus(new ServerStatus(state, int.MaxValue));
            Assert.True(_view.ToolTip!.Length <= 127, _view.ToolTip);
        }
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

    [Fact]
    public void New_DoesNotReadTheProfiles()
    {
        CreatePresenter();

        Assert.Equal(0, _profileStore.LoadCount);
    }

    [Fact]
    public void RefreshProfiles_ShowsTheNamesInAlphabeticalOrder()
    {
        _profileStore.Profiles = [new Profile("Werk", Uniform(StaticLighting(Red))), new Profile("gamen", Uniform(StaticLighting(Green)))];

        CreatePresenter().RefreshProfiles();

        Assert.Equal(["gamen", "Werk"], _view.Profiles);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public void RefreshProfiles_NoProfiles_ShowsAnEmptyList()
    {
        CreatePresenter().RefreshProfiles();

        Assert.Equal([], _view.Profiles);
    }

    // The settings app saves and deletes through the same library, so the next time the menu
    // opens it shows the new list.
    [Fact]
    public void RefreshProfiles_AfterAChangeFromTheApp_ShowsTheNewList()
    {
        _profileStore.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red)))];
        var presenter = CreatePresenter();
        presenter.RefreshProfiles();

        _profiles.Save("Nacht");
        _profiles.Delete("Gamen");
        presenter.RefreshProfiles();

        Assert.Equal(["Nacht"], _view.Profiles);
    }

    [Fact]
    public void RefreshProfiles_FileCannotBeRead_GreysOutTheMenuAndWarns()
    {
        _profileStore.LoadException = new IOException("The file is in use.");

        CreatePresenter().RefreshProfiles();

        Assert.Null(_view.Profiles);
        Assert.Equal([(Strings.ProfilesUnreadable, TrayNotificationKind.Warning)], _view.Notifications);
    }

    [Fact]
    public void RefreshProfiles_FileReadableAgain_ShowsTheProfiles()
    {
        _profileStore.LoadException = new UnauthorizedAccessException();
        var presenter = CreatePresenter();
        presenter.RefreshProfiles();

        _profileStore.LoadException = null;
        _profileStore.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red)))];
        presenter.RefreshProfiles();

        Assert.Equal(["Gamen"], _view.Profiles);
    }

    [Fact]
    public async Task LoadProfile_ShowsAndSavesItsLighting()
    {
        await _lighting.DiscoverAsync(Ct);
        _profileStore.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red, 0.1)))];
        var changes = 0;
        _lighting.LightingChanged += (_, _) => changes++;

        CreatePresenter().LoadProfile("Nacht");

        Assert.Equal(StaticLighting(Red, 0.1), _lighting.GetLighting(null));
        Assert.Equal(1, changes);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public void LoadProfile_DeletedSinceTheMenuOpened_WarnsWithoutThrowing()
    {
        _profileStore.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red)))];
        var presenter = CreatePresenter();
        presenter.RefreshProfiles();
        _profiles.Delete("Nacht");

        presenter.LoadProfile("Nacht");

        Assert.Equal([("Profiel 'Nacht' kon niet worden geladen.", TrayNotificationKind.Warning)], _view.Notifications);
    }

    private TrayPresenter CreatePresenter() => new(_host, _autoStart, _launcher, _profiles, _view);

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

        public IReadOnlyList<string>? Profiles { get; private set; } = ["not shown yet"];

        public List<(string Text, TrayNotificationKind Kind)> Notifications { get; } = [];

        public void SetToolTip(string text) => ToolTip = text;

        public void SetAutoStartChecked(bool isChecked) => AutoStartChecked = isChecked;

        public void ShowProfiles(IReadOnlyList<string>? names) => Profiles = names;

        public void ShowNotification(string text, TrayNotificationKind kind) => Notifications.Add((text, kind));
    }
}
