using System.Diagnostics;

namespace BuurmansRgb.Engine.SettingsApp;

internal sealed class SettingsAppLauncher(string executablePath) : ISettingsAppLauncher
{
    public const string ExecutableName = "BuurmansRgb.UI.exe";

    public static SettingsAppLauncher NextToEngine() => new(Path.Combine(AppContext.BaseDirectory, ExecutableName));

    public bool TryLaunch()
    {
        if (!File.Exists(executablePath))
        {
            return false;
        }

        using var process = Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
        return true;
    }
}
