using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.Restart;
using BuurmansRgb.Engine.SettingsApp;
using BuurmansRgb.Engine.Tray;
using BuurmansRgb.Providers.Simulated;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class TrayPresenterTests : IDisposable
{
    private readonly FakeEngineHost _host = new();
    private readonly FakeAutoStart _autoStart = new();
    private readonly AutoStartSwitch _autoStartSwitch;
    private readonly FakeSettingsAppLauncher _launcher = new();
    private readonly FakeTrayView _view = new();
    private readonly AllOnOffHotkeys _hotkeys = new();
    private readonly FakeGlobalHotkeys _globalHotkeys = new();
    private readonly InMemoryProfileStore _profileStore = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly ProfileLibrary _profiles;

    public TrayPresenterTests()
    {
        _profiles = new ProfileLibrary(_profileStore, _lighting);
        _autoStartSwitch = new AutoStartSwitch(_autoStart);
    }

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

    // The tray menu and the settings app share one switch: a click in the menu reaches the app
    // through OptionsChanged, with the new value.
    [Fact]
    public async Task SetAutoStart_InTheMenu_ReachesTheSettingsApp()
    {
        using var settingsApp = new SettingsAppSide(_lighting, _profiles, _autoStartSwitch);
        var changes = 0;
        settingsApp.Api.OptionsChanged += (_, _) => changes++;

        CreatePresenter().SetAutoStart(true);

        Assert.Equal(1, changes);
        Assert.True(await settingsApp.Api.GetAutoStartAsync(Ct));
    }

    // And the other way round: the menu reads it when it opens (RefreshAutoStart), so it shows
    // what the settings app set.
    [Fact]
    public async Task SetAutoStartAsync_InTheSettingsApp_ShowsInTheMenu()
    {
        using var settingsApp = new SettingsAppSide(_lighting, _profiles, _autoStartSwitch);
        var presenter = CreatePresenter();

        await settingsApp.Api.SetAutoStartAsync(true, Ct);
        presenter.RefreshAutoStart();
        Assert.True(_view.AutoStartChecked);

        await settingsApp.Api.SetAutoStartAsync(false, Ct);
        presenter.RefreshAutoStart();
        Assert.False(_view.AutoStartChecked);
        Assert.Empty(_view.Notifications);
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

    [Fact]
    public void ApplyHotkeys_BeforeTheEngineStarted_RegistersNothing()
    {
        CreatePresenter().ApplyHotkeys();

        Assert.Empty(_globalHotkeys.Registered);
        Assert.Equal((null, null), _view.Hotkeys);
    }

    [Fact]
    public async Task StartAsync_RegistersBothHotkeysAndShowsThemInTheMenu()
    {
        await StartedPresenterAsync();

        Assert.Equal([AllOnOffHotkeys.AllOn, AllOnOffHotkeys.AllOff], _globalHotkeys.Registered);
        Assert.Equal(("Ctrl+Alt+Shift+A", "Ctrl+Alt+Shift+U"), _view.Hotkeys);
        Assert.Empty(_view.Notifications);
    }

    [Fact]
    public async Task StartAsync_HotkeysOff_RegistersNothing()
    {
        _hotkeys.Enabled = false;

        await StartedPresenterAsync();

        Assert.Empty(_globalHotkeys.Registered);
        Assert.Equal((null, null), _view.Hotkeys);
    }

    [Fact]
    public async Task StartAsync_Failed_RegistersNothing()
    {
        _host.StartException = new IOException("Disk unavailable.");

        await CreatePresenter().StartAsync(Ct);

        Assert.Empty(_globalHotkeys.Registered);
    }

    [Fact]
    public async Task AllOffHotkey_DoesWhatAllesUitDoes()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.SetLighting(RecordingDeviceProvider.DeviceId, current => current with { Color = Red });
        var presenter = await StartedPresenterAsync();

        _globalHotkeys.Press(AllOnOffHotkeys.AllOff);
        presenter.RefreshAllOnOff();

        Assert.False(_lighting.GetLighting(null).On);
        Assert.False(_lighting.GetLighting(RecordingDeviceProvider.DeviceId).On);
        Assert.Equal((true, false), _view.AllOnOff);
    }

    [Fact]
    public async Task AllOnHotkey_DoesWhatAllesAanDoes()
    {
        await _lighting.DiscoverAsync(Ct);
        _lighting.SetLighting(null, current => current with { On = false });
        _lighting.SetLighting(RecordingDeviceProvider.DeviceId, current => current with { On = false });
        await StartedPresenterAsync();

        _globalHotkeys.Press(AllOnOffHotkeys.AllOn);

        Assert.True(_lighting.GetLighting(null).On);
        Assert.True(_lighting.GetLighting(RecordingDeviceProvider.DeviceId).On);
    }

    // The settings app turned them off: the keys go back to Windows and do nothing any more.
    [Fact]
    public async Task ApplyHotkeys_TurnedOff_GivesTheKeysBackAndHidesThem()
    {
        await _lighting.DiscoverAsync(Ct);
        var presenter = await StartedPresenterAsync();

        _hotkeys.Enabled = false;
        presenter.ApplyHotkeys();
        _globalHotkeys.Press(AllOnOffHotkeys.AllOff);

        Assert.Empty(_globalHotkeys.Registered);
        Assert.Equal((null, null), _view.Hotkeys);
        Assert.True(_lighting.GetLighting(null).On);
    }

    [Fact]
    public async Task ApplyHotkeys_TurnedOnAgain_RegistersThemAgain()
    {
        var presenter = await StartedPresenterAsync();
        _hotkeys.Enabled = false;
        presenter.ApplyHotkeys();

        _hotkeys.Enabled = true;
        presenter.ApplyHotkeys();

        Assert.Equal(2, _globalHotkeys.Registered.Count);
        Assert.Equal(("Ctrl+Alt+Shift+A", "Ctrl+Alt+Shift+U"), _view.Hotkeys);
    }

    [Fact]
    public async Task ApplyHotkeys_AgainWhileOn_DoesNotRegisterTwice()
    {
        var presenter = await StartedPresenterAsync();

        presenter.ApplyHotkeys();

        Assert.Equal(2, _globalHotkeys.RegisterCount);
    }

    // Another app has the key: the engine goes on, the other key works and the menu shows only that one.
    [Fact]
    public async Task StartAsync_OneHotkeyTaken_WarnsAndKeepsTheOther()
    {
        _globalHotkeys.Taken.Add(AllOnOffHotkeys.AllOn);

        await StartedPresenterAsync();

        Assert.Equal([AllOnOffHotkeys.AllOff], _globalHotkeys.Registered);
        Assert.Equal((null, "Ctrl+Alt+Shift+U"), _view.Hotkeys);
        Assert.Equal([("De sneltoets Ctrl+Alt+Shift+A werkt niet: een andere app gebruikt hem al.", TrayNotificationKind.Warning)], _view.Notifications);
    }

    [Fact]
    public async Task StartAsync_BothHotkeysTaken_WarnsOnce()
    {
        _globalHotkeys.Taken.UnionWith([AllOnOffHotkeys.AllOn, AllOnOffHotkeys.AllOff]);

        await StartedPresenterAsync();

        Assert.Equal((null, null), _view.Hotkeys);
        Assert.Equal([("De sneltoetsen Ctrl+Alt+Shift+A en Ctrl+Alt+Shift+U werken niet: een andere app gebruikt ze al.", TrayNotificationKind.Warning)], _view.Notifications);
    }

    // Turned on again after the other app closed, the key works after all.
    [Fact]
    public async Task ApplyHotkeys_KeyFreeAgain_RegistersIt()
    {
        _globalHotkeys.Taken.Add(AllOnOffHotkeys.AllOn);
        var presenter = await StartedPresenterAsync();

        _globalHotkeys.Taken.Clear();
        presenter.ApplyHotkeys();

        Assert.Equal(("Ctrl+Alt+Shift+A", "Ctrl+Alt+Shift+U"), _view.Hotkeys);
        Assert.Single(_view.Notifications);
    }

    // "Herstarten": the new engine started, so this one exits; the tooltip tells of it meanwhile.
    [Fact]
    public async Task RestartAsync_NewEngineStarted_ReturnsTrue()
    {
        var restart = new RestartFakes();
        restart.Services.Pending = new TaskCompletionSource<ServicesRestartReport>();
        var presenter = CreatePresenter(restarter: restart.Restarter);

        var restarting = presenter.RestartAsync(Ct);
        Assert.StartsWith($"{Strings.TrayToolTipRestarting}\n", _view.ToolTip, StringComparison.Ordinal);
        Assert.False(_view.RestartEnabled);
        restart.Services.Pending.SetResult(ServicesRestartReport.Declined);

        Assert.True(await restarting);
        Assert.Single(restart.Launcher.Starts);
        Assert.Empty(_view.Notifications);
        Assert.StartsWith($"{Strings.TrayToolTipRestarting}\n", _view.ToolTip, StringComparison.Ordinal);
        Assert.False(_view.RestartEnabled);
    }

    // A second click while the first runs does nothing, so there is one UAC prompt.
    [Fact]
    public async Task RestartAsync_WhileRestarting_DoesNothing()
    {
        var restart = new RestartFakes();
        restart.Services.Pending = new TaskCompletionSource<ServicesRestartReport>();
        var presenter = CreatePresenter(restarter: restart.Restarter);

        var first = presenter.RestartAsync(Ct);
        Assert.False(await presenter.RestartAsync(Ct));
        restart.Services.Pending.SetResult(ServicesRestartReport.NothingInstalled);

        Assert.True(await first);
        Assert.Equal(1, restart.Services.Count);
    }

    // Without a new engine this one keeps running: a warning, the settings app back, the tooltip
    // as it was.
    [Fact]
    public async Task RestartAsync_NewEngineDoesNotStart_WarnsAndKeepsRunning()
    {
        var restart = new RestartFakes();
        restart.Launcher.Result = false;
        restart.SettingsApp.IsOpen = true;
        var presenter = CreatePresenter(restarter: restart.Restarter);

        Assert.False(await presenter.RestartAsync(Ct));

        Assert.Equal([(Strings.RestartFailed, TrayNotificationKind.Warning)], _view.Notifications);
        Assert.Equal(1, _launcher.LaunchCount);
        Assert.Equal($"{Strings.AppName}\nOpenRGB: verbinden…", _view.ToolTip);
        Assert.True(_view.RestartEnabled);

        // It can be tried again.
        Assert.False(await presenter.RestartAsync(Ct));
        Assert.Equal(2, restart.Services.Count);
    }

    [Fact]
    public async Task RestartAsync_Throws_WarnsAndKeepsRunning()
    {
        var restart = new RestartFakes();
        restart.Services.Exception = new IOException("Broken.");
        var presenter = CreatePresenter(restarter: restart.Restarter);

        Assert.False(await presenter.RestartAsync(Ct));

        Assert.Equal([(Strings.RestartFailed, TrayNotificationKind.Warning)], _view.Notifications);
        Assert.Empty(restart.Launcher.Starts);
        Assert.True(_view.RestartEnabled);
    }

    [Fact]
    public async Task RestartAsync_WithoutRestarter_DoesNothing()
    {
        Assert.False(await CreatePresenter().RestartAsync(Ct));
        Assert.Empty(_view.Notifications);
    }

    // The new engine tells what restarted once it runs, and opens the settings app that was open.
    [Fact]
    public async Task StartAsync_AfterRestart_TellsWhatRestartedAndOpensTheSettingsApp()
    {
        var report = new ServicesRestartReport(ServiceOutcome.Restarted, ServiceOutcome.NotStopped);

        await CreatePresenter(restartedWith: report, openSettingsAfterStart: true).StartAsync(Ct);

        Assert.Equal([TrayPresenter.DescribeRestart(report)], _view.Notifications);
        Assert.Equal(1, _launcher.LaunchCount);
    }

    [Fact]
    public async Task StartAsync_AfterRestartWithoutSettingsApp_OpensNothing()
    {
        await CreatePresenter(restartedWith: ServicesRestartReport.Declined).StartAsync(Ct);

        Assert.Equal([(Strings.RestartDeclined, TrayNotificationKind.Warning)], _view.Notifications);
        Assert.Equal(0, _launcher.LaunchCount);
    }

    // The error of the start counts more; the settings app opens anyway.
    [Fact]
    public async Task StartAsync_AfterRestart_StartFails_ShowsTheError()
    {
        _host.StartException = new IOException("Disk unavailable.");

        await CreatePresenter(restartedWith: ServicesRestartReport.NothingInstalled, openSettingsAfterStart: true).StartAsync(Ct);

        Assert.Equal([(Strings.StartupFailed, TrayNotificationKind.Error)], _view.Notifications);
        Assert.Equal(1, _launcher.LaunchCount);
    }

    [Fact]
    public void DescribeRestart_AllRestarted_IsInfo()
    {
        var (text, kind) = TrayPresenter.DescribeRestart(new(ServiceOutcome.Restarted, ServiceOutcome.Restarted));

        Assert.Equal($"{Strings.RestartDone}\n{Strings.RestartOpenRgbRestarted}\n{Strings.RestartPawnIoRestarted}", text);
        Assert.Equal(TrayNotificationKind.Info, kind);
    }

    [Fact]
    public void DescribeRestart_NothingInstalled_IsInfo()
    {
        var (text, kind) = TrayPresenter.DescribeRestart(ServicesRestartReport.NothingInstalled);

        Assert.Equal($"{Strings.RestartDone}\n{Strings.RestartOpenRgbNotInstalled}\n{Strings.RestartPawnIoNotInstalled}", text);
        Assert.Equal(TrayNotificationKind.Info, kind);
    }

    [Theory]
    [InlineData((int)ServiceOutcome.Restarted, (int)ServiceOutcome.NotStopped)]
    [InlineData((int)ServiceOutcome.Restarted, (int)ServiceOutcome.Failed)]
    [InlineData((int)ServiceOutcome.Failed, (int)ServiceOutcome.NotStopped)]
    [InlineData((int)ServiceOutcome.Failed, (int)ServiceOutcome.Restarted)]
    public void DescribeRestart_SomethingDidNotRestart_IsAWarning(int openRgb, int pawnIo)
    {
        var (text, kind) = TrayPresenter.DescribeRestart(new((ServiceOutcome)openRgb, (ServiceOutcome)pawnIo));

        Assert.StartsWith($"{Strings.RestartDone}\n", text, StringComparison.Ordinal);
        Assert.Equal(TrayNotificationKind.Warning, kind);
    }

    // PawnIO that was not tried, as OpenRGB did not stop, says so instead of blaming another program.
    [Fact]
    public void DescribeRestart_PawnIoSkippedForOpenRgb_SaysWhy()
    {
        var (text, kind) = TrayPresenter.DescribeRestart(new(ServiceOutcome.Failed, ServiceOutcome.SkippedForOpenRgb));

        Assert.Equal($"{Strings.RestartDone}\n{Strings.RestartOpenRgbFailed}\n{Strings.RestartPawnIoSkippedForOpenRgb}", text);
        Assert.Equal(TrayNotificationKind.Warning, kind);
    }

    [Fact]
    public void DescribeRestart_DeclinedOrFailedStep_IsOneLine()
    {
        Assert.Equal((Strings.RestartDeclined, TrayNotificationKind.Warning), TrayPresenter.DescribeRestart(ServicesRestartReport.Declined));
        Assert.Equal((Strings.RestartServicesFailed, TrayNotificationKind.Warning), TrayPresenter.DescribeRestart(ServicesRestartReport.Failed));
    }

    private TrayPresenter CreatePresenter(
        Action? openLog = null,
        EngineRestarter? restarter = null,
        ServicesRestartReport? restartedWith = null,
        bool openSettingsAfterStart = false) =>
        new(_host, _autoStartSwitch, _launcher, _profiles, _lighting, _hotkeys, _globalHotkeys, openLog ?? (() => { }), _view, restarter, restartedWith, openSettingsAfterStart);

    // The engine as a settings app talks to it, beside the tray menu of the same engine.
    private sealed class SettingsAppSide : IDisposable
    {
        private readonly ZoneLengthKeeper _zoneLengths;
        private readonly SimulatedDevicesSwitch _simulatedDevices;

        public SettingsAppSide(LightingController lighting, ProfileLibrary profiles, AutoStartSwitch autoStart)
        {
            _zoneLengths = new ZoneLengthKeeper(lighting);
            _simulatedDevices = new SimulatedDevicesSwitch(SwitchableDeviceProvider.For(new SimulatedDeviceProvider()), lighting);
            Api = new EngineApi(lighting, _zoneLengths, profiles, _simulatedDevices, new FakeServerStatus(), autoStart: autoStart);
        }

        public EngineApi Api { get; }

        public void Dispose()
        {
            Api.Dispose();
            _simulatedDevices.Dispose();
            _zoneLengths.Dispose();
        }
    }

    private sealed class RestartFakes
    {
        public RestartFakes() => Restarter = new EngineRestarter(Services, SettingsApp, Launcher);

        public EngineRestarterTests.FakeServicesRestart Services { get; } = new();

        public EngineRestarterTests.FakeSettingsAppWindow SettingsApp { get; } = new();

        public EngineRestarterTests.FakeEngineLauncher Launcher { get; } = new();

        public EngineRestarter Restarter { get; }
    }

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

        public (string? AllOn, string? AllOff) Hotkeys { get; private set; }

        public void ShowAllOnOffHotkeys(string? allOn, string? allOff) => Hotkeys = (allOn, allOff);

        public void ShowNotification(string text, TrayNotificationKind kind) => Notifications.Add((text, kind));

        public bool RestartEnabled { get; private set; } = true;

        public void SetRestartEnabled(bool enabled) => RestartEnabled = enabled;
    }
}
