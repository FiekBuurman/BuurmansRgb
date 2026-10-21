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

    // Only counts as enabled when it starts this executable; FollowMove takes over a moved install.
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName) is string value && string.Equals(value, command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string? FollowMove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        if (key?.GetValue(valueName) is not string value
            || string.Equals(value, command, StringComparison.OrdinalIgnoreCase)
            || File.Exists(ExecutableOf(value)))
        {
            return null;
        }

        key.SetValue(valueName, command);
        return value;
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

    // The engine writes its path in quotes; a value without them is taken as a whole path.
    private static string ExecutableOf(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('"'))
        {
            return trimmed;
        }

        var end = trimmed.IndexOf('"', 1);
        return end < 0 ? trimmed[1..] : trimmed[1..end];
    }
}
