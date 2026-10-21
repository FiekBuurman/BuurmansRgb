using BuurmansRgb.Contracts;
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
    public void SetPaused_PausesAndResumesTheHost()
    {
        var presenter = CreatePresenter();

        presenter.SetPaused(true);
        Assert.True(_host.Paused);

        presenter.SetPaused(false);
        Assert.False(_host.Paused);
    }

    [Fact]
    public void ShowPause_Manual_ChecksTheMenuItemAndSwitchesToolTip()
    {
        var presenter = CreatePresenter();

        presenter.ShowPause(PauseReasons.Manual);
        Assert.True(_view.PauseChecked);
        Assert.Equal($"{Strings.TrayToolTipPaused}\nOpenRGB: verbinden…", _view.ToolTip);

        presenter.ShowPause(PauseReasons.None);
        Assert.False(_view.PauseChecked);
        Assert.Equal($"{Strings.AppName}\nOpenRGB: verbinden…", _view.ToolTip);
    }

    // The menu item is the pause from the menu only; the tooltip tells any pause.
    [Theory]
    [InlineData(PauseReasons.Locked, false)]
    [InlineData(PauseReasons.Asleep, false)]
    [InlineData(PauseReasons.Manual | PauseReasons.Locked, true)]
    public void ShowPause_OtherReasons_CheckTheMenuItemOnlyForTheManualPause(PauseReasons pause, bool isChecked)
    {
        CreatePresenter().ShowPause(pause);

        Assert.Equal(isChecked, _view.PauseChecked);
        Assert.StartsWith(Strings.TrayToolTipPaused + "\n", _view.ToolTip);
    }

    // The settings app ended the pause: the engine reports it, and the menu item follows.
    [Fact]
    public void ShowPause_EndedElsewhere_UnchecksTheMenuItem()
    {
        var presenter = CreatePresenter();
        presenter.SetPaused(true);
        presenter.ShowPause(_host.Pause);

        presenter.ShowPause(PauseReasons.None);

        Assert.False(_view.PauseChecked);
        Assert.StartsWith(Strings.AppName + "\n", _view.ToolTip);
    }

    // The same texts as in OpenRgbStatusViewModelTests, so the tray and the app say the same.
    [Theory]
    [InlineData(ServerState.Connected, 4, "OpenRGB: verbonden, 4 lampen")]
    [InlineData(ServerState.Connected, 1, "OpenRGB: verbonden, 1 lamp")]
    [InlineData(ServerState.Connected, 0, "OpenRGB: verbonden, geen lampen gevonden")]
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
        presenter.ShowPause(PauseReasons.Manual);

        presenter.ShowOpenRgbStatus(new ServerStatus(ServerState.Unreachable));

        Assert.StartsWith(Strings.TrayToolTipPaused + "\n", _view.ToolTip);
    }

    // NotifyIcon refuses a tooltip of more than 127 characters.
    [Fact]
    public void ToolTip_FitsTheTrayIconInEveryState()
    {
        var presenter = CreatePresenter();
        presenter.ShowPause(PauseReasons.Manual);

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

    // The saved lighting that the start puts on would overwrite a profile loaded before it.
    [Fact]
    public void RefreshProfiles_BeforeTheEngineStarted_GreysOutTheMenuWithoutReading()
    {
        CreatePresenter().RefreshProfiles();

        Assert.Null(_view.Profiles);
        Assert.Equal(0, _profileStore.LoadCount);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public async Task RefreshProfiles_StartFailed_KeepsTheMenuGreyedOut()
    {
        _host.StartException = new IOException("Disk unavailable.");
        var presenter = CreatePresenter();
        await presenter.StartAsync(Ct);

        presenter.RefreshProfiles();

        Assert.Null(_view.Profiles);
    }

    [Fact]
    public async Task RefreshProfiles_ShowsTheNamesInAlphabeticalOrder()
    {
        _profileStore.Profiles = [new Profile("Werk", Uniform(StaticLighting(Red))), new Profile("gamen", Uniform(StaticLighting(Green)))];

        (await StartedPresenterAsync()).RefreshProfiles();

        Assert.Equal(["gamen", "Werk"], _view.Profiles);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public async Task RefreshProfiles_ChecksTheSceneThatIsOn()
    {
        _profileStore.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red))), new Profile("Nacht", Uniform(StaticLighting(Green)))];
        var presenter = await StartedPresenterAsync();

        presenter.LoadProfile("Nacht");
        presenter.RefreshProfiles();
        Assert.Equal("Nacht", _view.ActiveProfile);

        _lighting.SetLighting(null, lighting => lighting with { Brightness = 0.5 });
        presenter.RefreshProfiles();
        Assert.Null(_view.ActiveProfile);
    }

    [Fact]
    public async Task RefreshProfiles_NoProfiles_ShowsAnEmptyList()
    {
        (await StartedPresenterAsync()).RefreshProfiles();

        Assert.Equal([], _view.Profiles);
    }

    // The settings app saves and deletes through the same library, so the next time the menu
    // opens it shows the new list.
    [Fact]
    public async Task RefreshProfiles_AfterAChangeFromTheApp_ShowsTheNewList()
    {
        _profileStore.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red)))];
        var presenter = await StartedPresenterAsync();
        presenter.RefreshProfiles();

        _profiles.Save("Nacht");
        _profiles.Delete("Gamen");
        presenter.RefreshProfiles();

        Assert.Equal(["Nacht"], _view.Profiles);
    }

    [Fact]
    public async Task RefreshProfiles_FileCannotBeRead_GreysOutTheMenuAndWarns()
    {
        _profileStore.LoadException = new IOException("The file is in use.");

        (await StartedPresenterAsync()).RefreshProfiles();

        Assert.Null(_view.Profiles);
        Assert.Equal([(Strings.ProfilesUnreadable, TrayNotificationKind.Warning)], _view.Notifications);
    }

    [Fact]
    public async Task RefreshProfiles_FileReadableAgain_ShowsTheProfiles()
    {
        _profileStore.LoadException = new UnauthorizedAccessException();
        var presenter = await StartedPresenterAsync();
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
    public async Task LoadProfile_DeletedSinceTheMenuOpened_WarnsWithoutThrowing()
    {
        _profileStore.Profiles = [new Profile("Nacht", Uniform(StaticLighting(Red)))];
        var presenter = await StartedPresenterAsync();
        presenter.RefreshProfiles();
        _profiles.Delete("Nacht");

        presenter.LoadProfile("Nacht");

        Assert.Equal([("Scène 'Nacht' kon niet worden aangezet.", TrayNotificationKind.Warning)], _view.Notifications);
    }

    [Fact]
    public void RefreshAllOnOff_BeforeTheEngineStarted_GreysOutBoth()
    {
        CreatePresenter().RefreshAllOnOff();

        Assert.Equal((false, false), _view.AllOnOff);
    }

    [Fact]
    public async Task RefreshAllOnOff_AllOn_OffersOnlyAllOff()
    {
        await _lighting.DiscoverAsync(Ct);
        var presenter = await StartedPresenterAsync();

        presenter.RefreshAllOnOff();

        Assert.Equal((false, true), _view.AllOnOff);
    }

    [Fact]
    public async Task SetAllOn_False_SwitchesEveryLightOff_AndThenOffersOnlyAllOn()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.SetLighting(RecordingDeviceProvider.DeviceId, current => current with { Color = Red });
        var presenter = await StartedPresenterAsync();

        presenter.SetAllOn(false);
        presenter.RefreshAllOnOff();

        Assert.False(_lighting.GetLighting(null).On);
        Assert.False(_lighting.GetLighting(RecordingDeviceProvider.DeviceId).On);
        Assert.Equal((true, false), _view.AllOnOff);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public async Task RefreshAllOnOff_OneLightOff_OffersBoth()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.SetLighting(RecordingDeviceProvider.DeviceId, current => current with { On = false });
        var presenter = await StartedPresenterAsync();

        presenter.RefreshAllOnOff();

        Assert.Equal((true, true), _view.AllOnOff);
    }

    // The saved lighting that the engine puts on next would overwrite it.
    [Fact]
    public async Task SetAllOn_BeforeTheEngineStarted_DoesNothing()
    {
        await _lighting.DiscoverAsync(Ct);

        CreatePresenter().SetAllOn(false);

        Assert.True(_lighting.GetLighting(null).On);
    }

    [Fact]
    public void OpenLog_Opens_ShowsNothing()
    {
        var opened = 0;

        CreatePresenter(() => opened++).OpenLog();

        Assert.Equal(1, opened);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public void OpenLog_NoAppOrNoFile_Warns()
    {
        CreatePresenter(() => throw new System.ComponentModel.Win32Exception(2)).OpenLog();

        Assert.Equal([(Strings.LogOpenFailed, TrayNotificationKind.Warning)], _view.Notifications);
    }

    private TrayPresenter CreatePresenter(Action? openLog = null) =>
        new(_host, _autoStart, _launcher, _profiles, _lighting, openLog ?? (() => { }), _view);

    private async Task<TrayPresenter> StartedPresenterAsync()
    {
        var presenter = CreatePresenter();
        await presenter.StartAsync(Ct);
        return presenter;
    }

    private sealed class FakeEngineHost : IEngineHost
    {
        public Exception? StartException { get; set; }

        public bool Started { get; private set; }

        public bool Paused => Pause.HasFlag(PauseReasons.Manual);

        public PauseReasons Pause { get; private set; }

        public event EventHandler<PauseReasons>? PauseChanged;

        public Task StartAsync(CancellationToken ct)
        {
            if (StartException is not null)
            {
                return Task.FromException(StartException);
            }

            Started = true;
            return Task.CompletedTask;
        }

        // Like the engine: the change comes back through PauseChanged.
        public void SetPaused(bool paused)
        {
            Pause = paused ? PauseReasons.Manual : PauseReasons.None;
            PauseChanged?.Invoke(this, Pause);
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

        public bool PauseChecked { get; private set; }

        public IReadOnlyList<string>? Profiles { get; private set; } = ["not shown yet"];

        public List<(string Text, TrayNotificationKind Kind)> Notifications { get; } = [];

        public void SetToolTip(string text) => ToolTip = text;

        public void SetAutoStartChecked(bool isChecked) => AutoStartChecked = isChecked;

        public void SetPauseChecked(bool isChecked) => PauseChecked = isChecked;

        public string? ActiveProfile { get; private set; }

        public void ShowProfiles(IReadOnlyList<string>? names, string? active)
        {
            Profiles = names;
            ActiveProfile = active;
        }

        public (bool CanSwitchOn, bool CanSwitchOff) AllOnOff { get; private set; } = (true, true);

        public void ShowAllOnOff(bool canSwitchOn, bool canSwitchOff) => AllOnOff = (canSwitchOn, canSwitchOff);

        public void ShowNotification(string text, TrayNotificationKind kind) => Notifications.Add((text, kind));
    }
}
