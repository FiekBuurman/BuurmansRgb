using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BuurmansRgb.UI.Services;

// The contrast theme of Windows, as WPF reads it (SystemParameters.HighContrast). WPF says it
// changed when Windows turns a contrast theme on or off, and also when another theme takes over,
// as long as it was read since: IsOn reads it each time. The colors of a theme can change without
// that, such as when you edit them, and ContrastTheme keeps the brushes of SystemColors it found,
// so a change of the colors of Windows counts as a change too; without a contrast theme
// ContrastTheme then has nothing to do. HighContrastMessages turns those messages into one Changed
// on the UI thread.
internal sealed class SystemHighContrast : IHighContrast, IDisposable
{
    private readonly HighContrastMessages _messages = new(Dispatcher.CurrentDispatcher);

    public SystemHighContrast()
    {
        _messages.Changed += OnChanged;
        SystemParameters.StaticPropertyChanged += _messages.OnSystemParameterChanged;
        SystemEvents.UserPreferenceChanged += _messages.OnUserPreferenceChanged;
    }

    public event EventHandler? Changed;

    public bool IsOn => SystemParameters.HighContrast;

    public void Dispose()
    {
        SystemParameters.StaticPropertyChanged -= _messages.OnSystemParameterChanged;
        SystemEvents.UserPreferenceChanged -= _messages.OnUserPreferenceChanged;
        _messages.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, e);
}
