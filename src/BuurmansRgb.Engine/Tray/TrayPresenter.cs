using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Engine.SettingsApp;

namespace BuurmansRgb.Engine.Tray;

// The tray menu logic without WinForms types, so it can be tested with fakes. Call it on the tray
// thread only.
internal sealed class TrayPresenter(
    IEngineHost host,
    IAutoStart autoStart,
    ISettingsAppLauncher settingsAppLauncher,
    ProfileLibrary profiles,
    LightingController lighting,
    AllOnOffHotkeys hotkeys,
    IGlobalHotkeys globalHotkeys,
    Action openLog,
    ITrayView view)
{
    private PauseReasons _pause;
    private ServerStatus _openRgbStatus = ServerStatus.Connecting;

    // Profiles can be loaded once the engine has put on the saved lighting, which would overwrite a
    // profile loaded before it, and keeps what is loaded from then on.
    private bool _started;

    // Never throws, so the caller can start it without awaiting.
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await host.StartAsync(ct);
            _started = true;
            ApplyHotkeys();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The engine is shutting down; nothing to report.
        }
        catch (Exception exception)
        {
            ReportFailure(exception, Strings.StartupFailed, TrayNotificationKind.Error);
        }
    }

    public void OpenSettings()
    {
        switch (settingsAppLauncher.Launch())
        {
            case SettingsAppLaunchResult.NotFound:
                view.ShowNotification(Strings.SettingsAppNotFound, TrayNotificationKind.Info);
                break;
            case SettingsAppLaunchResult.Failed:
                view.ShowNotification(Strings.SettingsAppLaunchFailed, TrayNotificationKind.Warning);
                break;
        }
    }

    // The menu follows through ShowPause, like a pause from elsewhere.
    public void SetPaused(bool paused) => host.SetPaused(paused);

    // The pause as the engine has it, also after the settings app ended it or Windows was locked.
    // The menu item shows the pause from the menu; the tooltip tells any pause.
    public void ShowPause(PauseReasons pause)
    {
        _pause = pause;
        view.SetPauseChecked(pause.HasFlag(PauseReasons.Manual));
        ShowToolTip();
    }

    public void ShowOpenRgbStatus(ServerStatus status)
    {
        _openRgbStatus = status;
        ShowToolTip();
    }

    public void RefreshAutoStart() => view.SetAutoStartChecked(autoStart.IsEnabled);

    // Called when the menu opens, so the list is the current one, also after the settings app
    // saved or deleted a profile. Until the engine has started, the item is greyed out.
    public void RefreshProfiles()
    {
        if (!_started)
        {
            view.ShowProfiles(null, null);
            return;
        }

        try
        {
            view.ShowProfiles(profiles.Names, profiles.ActiveName);
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            view.ShowProfiles(null, null);
            ReportFailure(exception, Strings.ProfilesUnreadable, TrayNotificationKind.Warning);
        }
    }

    // Any failure is reported, so a click in the menu never takes the engine down: the profile can
    // be gone since the menu opened, or a device can fail.
    public void LoadProfile(string name)
    {
        try
        {
            profiles.Load(name);
        }
        catch (Exception exception)
        {
            ReportFailure(exception, string.Format(CultureInfo.CurrentCulture, Strings.ProfileLoadFailed, name), TrayNotificationKind.Warning);
        }
    }

    // Called when the menu opens. Everything is on when all lights are, also the ones with lighting
    // of their own; switched on again, each shows its own lighting.
    public void RefreshAllOnOff()
    {
        if (!_started)
        {
            view.ShowAllOnOff(canSwitchOn: false, canSwitchOff: false);
            return;
        }

        var state = lighting.State;
        IEnumerable<DeviceLighting> all = [state.Global, .. state.Devices.Values];
        view.ShowAllOnOff(canSwitchOn: all.Any(device => !device.On), canSwitchOff: all.Any(device => device.On));
    }

    // Like a click on "Alles aan of uit" in the settings app. Reported like a profile that fails.
    public void SetAllOn(bool on)
    {
        if (!_started)
        {
            return;
        }

        try
        {
            lighting.SetLighting(null, current => current with { On = on });
        }
        catch (Exception exception)
        {
            ReportFailure(exception, on ? Strings.AllOnFailed : Strings.AllOffFailed, TrayNotificationKind.Warning);
        }
    }

    // Registers the hotkeys of "Alles aan" and "Alles uit" while they are on, from the start of the
    // engine on (before it they would do nothing, as the menu items), and gives them back when they
    // are turned off. Called again when hotkeys.Enabled changes. A key another app has already
    // stays out, which a notification tells; the next call tries it again. The menu shows a key
    // next to its item only while it works.
    public void ApplyHotkeys()
    {
        if (_started && hotkeys.Enabled)
        {
            var taken = new List<string>();
            RegisterHotkey(AllOnOffHotkeys.AllOn, on: true, taken);
            RegisterHotkey(AllOnOffHotkeys.AllOff, on: false, taken);
            if (taken.Count > 0)
            {
                var text = taken.Count == 1
                    ? string.Format(CultureInfo.CurrentCulture, Strings.HotkeyTaken, taken[0])
                    : string.Format(CultureInfo.CurrentCulture, Strings.HotkeysTaken, taken[0], taken[1]);
                view.ShowNotification(text, TrayNotificationKind.Warning);
            }
        }
        else
        {
            globalHotkeys.Unregister(AllOnOffHotkeys.AllOn);
            globalHotkeys.Unregister(AllOnOffHotkeys.AllOff);
        }

        view.ShowAllOnOffHotkeys(HotkeyText(AllOnOffHotkeys.AllOn), HotkeyText(AllOnOffHotkeys.AllOff));
    }

    // The log of the engine in the app that opens .log files, Notepad by default.
    public void OpenLog()
    {
        try
        {
            openLog();
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            ReportFailure(exception, Strings.LogOpenFailed, TrayNotificationKind.Warning);
        }
    }

    public void SetAutoStart(bool enabled)
    {
        try
        {
            autoStart.SetEnabled(enabled);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            RefreshAutoStart();
            ReportFailure(exception, Strings.AutoStartFailed, TrayNotificationKind.Warning);
        }
    }

    // A pressed key does what its menu item does. A key that is registered already stays so.
    private void RegisterHotkey(Keys key, bool on, List<string> taken)
    {
        if (!globalHotkeys.IsRegistered(key) && !globalHotkeys.Register(key, () => SetAllOn(on)))
        {
            taken.Add(AllOnOffHotkeys.DisplayText(key));
        }
    }

    private string? HotkeyText(Keys key) => globalHotkeys.IsRegistered(key) ? AllOnOffHotkeys.DisplayText(key) : null;

    // The first line tells whether the effects run, the second how it goes with OpenRGB.
    private void ShowToolTip() =>
        view.SetToolTip($"{(_pause != PauseReasons.None ? Strings.TrayToolTipPaused : Strings.AppName)}\n{OpenRgbText(_openRgbStatus)}");

    private static string OpenRgbText(ServerStatus status) => status.State switch
    {
        ServerState.Connected when status.DeviceCount == 0 => Strings.TrayOpenRgbConnectedNoDevices,
        ServerState.Connected when status.DeviceCount == 1 => Strings.TrayOpenRgbConnectedOneDevice,
        ServerState.Connected => string.Format(CultureInfo.CurrentCulture, Strings.TrayOpenRgbConnected, status.DeviceCount),
        ServerState.Unreachable => Strings.TrayOpenRgbUnreachable,
        ServerState.Failed => Strings.TrayOpenRgbFailed,
        _ => Strings.TrayOpenRgbConnecting,
    };

    // .NET exception messages are English, so the user only sees the Dutch text; the details
    // go to the log ("Logbestand openen").
    private void ReportFailure(Exception exception, string text, TrayNotificationKind kind)
    {
        Trace.TraceError(exception.ToString());
        view.ShowNotification(text, kind);
    }
}
