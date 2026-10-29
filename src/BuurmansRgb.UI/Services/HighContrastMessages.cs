using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BuurmansRgb.UI.Services;

// The messages of Windows that can change the colors of a contrast theme, as one Changed on the
// dispatcher (see SystemParameterMessages): a change of SystemParameters.HighContrast, and a change
// of the colors of Windows (UserPreferenceChanged with Color, for WM_SYSCOLORCHANGE); other
// parameters and categories do not count. SystemEvents may tell from a thread of its own.
// SystemHighContrast hands it the messages; a test can hand it its own.
internal sealed class HighContrastMessages(Dispatcher dispatcher)
    : SystemParameterMessages(dispatcher, nameof(SystemParameters.HighContrast))
{
    public void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Color)
        {
            Post();
        }
    }
}
