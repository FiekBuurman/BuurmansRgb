using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The bottom of Instellingen: "Logbestand openen", as in the tray menu. The app opens the log of
// the engine itself, where the engine writes it (FileLog.PathOf), instead of asking the engine:
// so it also works when the engine does not run or does not answer, which is when the log is
// needed most, and the editor comes to the front, as this app has the focus. open opens a file or
// web address as Verkenner does (ShellLauncher.Open); tests pass a fake.
public sealed partial class AppInfoViewModel(Action<string> open, string engineLogPath)
{
    // Raised with the message for the status line when something could not be opened.
    public event EventHandler<string>? Failed;

    [RelayCommand]
    private void OpenLog() => Open(engineLogPath, Strings.LogOpenFailed);

    // As the tray menu, a log that is not there (yet), or no app for it, gives a message; the
    // details go to the log of this app.
    private void Open(string target, string failure)
    {
        try
        {
            open(target);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            Trace.TraceError(exception.ToString());
            Failed?.Invoke(this, failure);
        }
    }
}
