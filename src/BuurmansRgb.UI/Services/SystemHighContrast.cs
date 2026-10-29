using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BuurmansRgb.UI.Services;

// The contrast theme of Windows, as WPF reads it (SystemParameters.HighContrast). WPF says it
// changed when Windows turns a contrast theme on or off, and also when another theme takes over,
// as long as it was read since: IsOn reads it each time. The colors of a theme can change without
// that, such as when you edit them, and ContrastTheme keeps the brushes of SystemColors it found,
// so a change of the colors of Windows (UserPreferenceChanged with Color, for WM_SYSCOLORCHANGE)
// counts as a change too; without a contrast theme ContrastTheme then has nothing to do. Changed
// comes a little later, once the dispatcher has nothing more urgent to do, so WPF has read the new
// SystemColors by then, and it comes once for the burst of messages Windows sends for one change.
// SystemEvents may tell from a thread of its own, so the flag for that burst is set atomically.
internal sealed class SystemHighContrast : IHighContrast, IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private int _pending;

    public SystemHighContrast()
    {
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? Changed;

    public bool IsOn => SystemParameters.HighContrast;

    public void Dispose()
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
        {
            Post();
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Color)
        {
            Post();
        }
    }

    private void Post()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return;
        }

        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            Volatile.Write(ref _pending, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }
}
