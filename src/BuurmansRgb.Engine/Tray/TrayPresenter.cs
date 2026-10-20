using System.Diagnostics;
using System.Globalization;
using System.Security;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.Profiles;
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
    ITrayView view)
{
    private bool _paused;
    private ServerStatus _openRgbStatus = ServerStatus.Connecting;

    // Never throws, so the caller can start it without awaiting.
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await host.StartAsync(ct);
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

    public void SetPaused(bool paused)
    {
        host.SetPaused(paused);
        _paused = paused;
        ShowToolTip();
    }

    public void ShowOpenRgbStatus(ServerStatus status)
    {
        _openRgbStatus = status;
        ShowToolTip();
    }

    public void RefreshAutoStart() => view.SetAutoStartChecked(autoStart.IsEnabled);

    // Called when the menu opens, so the list is the current one, also after the settings app
    // saved or deleted a profile.
    public void RefreshProfiles()
    {
        try
        {
            view.ShowProfiles(profiles.Names);
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            view.ShowProfiles(null);
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

    // The first line tells whether the effects run, the second how it goes with OpenRGB.
    private void ShowToolTip() =>
        view.SetToolTip($"{(_paused ? Strings.TrayToolTipPaused : Strings.AppName)}\n{OpenRgbText(_openRgbStatus)}");

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
    // go to the debug output (DebugView) until the engine logs to a file.
    private void ReportFailure(Exception exception, string text, TrayNotificationKind kind)
    {
        Trace.TraceError(exception.ToString());
        view.ShowNotification(text, kind);
    }
}
