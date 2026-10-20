using Microsoft.Win32;

namespace BuurmansRgb.Engine.AutoStart;

internal sealed class RegistryAutoStart(string keyPath, string valueName, string command) : IAutoStart
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static RegistryAutoStart ForCurrentProcess()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The engine executable path is unknown.");
        return new(RunKeyPath, "BuurmansRgb", $"\"{executable}\"");
    }

    // Only counts as enabled when it starts this executable, so a moved install shows as off.
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) is string value && string.Equals(value, command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        if (enabled)
        {
            key.SetValue(valueName, command);
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
