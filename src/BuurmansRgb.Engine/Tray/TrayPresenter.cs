using System.Security;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.SettingsApp;

namespace BuurmansRgb.Engine.Tray;

// The tray menu logic without WinForms types, so it can be tested with fakes.
internal sealed class TrayPresenter(
    IEngineHost host,
    IAutoStart autoStart,
    ISettingsAppLauncher settingsAppLauncher,
    ITrayView view)
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await host.StartAsync(ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            view.ShowNotification(string.Format(Strings.StartupFailed, exception.Message), TrayNotificationKind.Error);
        }
    }

    public void OpenSettings()
    {
        if (!settingsAppLauncher.TryLaunch())
        {
            view.ShowNotification(Strings.SettingsAppNotFound, TrayNotificationKind.Info);
        }
    }

    public void SetPaused(bool paused)
    {
        host.SetPaused(paused);
        view.SetToolTip(paused ? Strings.TrayToolTipPaused : Strings.AppName);
    }

    public void RefreshAutoStart() => view.SetAutoStartChecked(autoStart.IsEnabled);

    public void SetAutoStart(bool enabled)
    {
        try
        {
            autoStart.SetEnabled(enabled);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            RefreshAutoStart();
            view.ShowNotification(string.Format(Strings.AutoStartFailed, exception.Message), TrayNotificationKind.Warning);
        }
    }
}
