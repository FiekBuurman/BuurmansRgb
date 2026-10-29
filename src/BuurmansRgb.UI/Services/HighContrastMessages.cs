using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BuurmansRgb.UI.Services;

// The messages of Windows that can change the colors of a contrast theme, as one Changed on the
// dispatcher: a change of SystemParameters.HighContrast, and a change of the colors of Windows
// (UserPreferenceChanged with Color, for WM_SYSCOLORCHANGE); other parameters and categories do not
// count. Changed comes a little later, once the dispatcher has nothing more urgent to do, so WPF has
// read the new SystemColors by then, and it comes once for the burst of messages Windows sends for
// one change. SystemEvents may tell from a thread of its own, so the flag for that burst is set
// atomically. SystemHighContrast hands it the messages; a test can hand it its own.
internal sealed class HighContrastMessages(Dispatcher dispatcher)
{
    private int _pending;

    public event EventHandler? Changed;

    public void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
        {
            Post();
        }
    }

    public void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
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

        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            Volatile.Write(ref _pending, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }
}
