using System.Diagnostics;
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
            ReportFailure(exception, Strings.AutoStartFailed, TrayNotificationKind.Warning);
        }
    }

    // .NET exception messages are English, so the user only sees the Dutch text; the details
    // go to the debug output (DebugView) until the engine logs to a file.
    private void ReportFailure(Exception exception, string text, TrayNotificationKind kind)
    {
        Trace.TraceError(exception.ToString());
        view.ShowNotification(text, kind);
    }
}
