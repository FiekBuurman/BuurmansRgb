using System.ComponentModel;
using System.Diagnostics;

namespace BuurmansRgb.Engine.SettingsApp;

internal sealed class SettingsAppLauncher(string executablePath) : ISettingsAppLauncher
{
    public const string ExecutableName = "BuurmansRgb.UI.exe";

    public static SettingsAppLauncher NextToEngine() => new(Path.Combine(AppContext.BaseDirectory, ExecutableName));

    public SettingsAppLaunchResult Launch()
    {
        if (!File.Exists(executablePath))
        {
            return SettingsAppLaunchResult.NotFound;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
            return SettingsAppLaunchResult.Launched;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // For example blocked by SmartScreen or AppLocker, not a valid executable, or UAC cancelled.
            Trace.TraceError(exception.ToString());
            return SettingsAppLaunchResult.Failed;
        }
    }
}
