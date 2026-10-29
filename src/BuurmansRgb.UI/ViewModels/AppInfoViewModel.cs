using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The bottom of Instellingen: "Logbestand openen", as in the tray menu, and the version of
// buurmansRgb with a link to the guide. The app opens the log of the engine itself, where the
// engine writes it (FileLog.PathOf), instead of asking the engine: so it also works when the
// engine does not run or does not answer, which is when the log is needed most, and the editor
// comes to the front, as this app has the focus. open opens a file or web address as Verkenner
// does (ShellLauncher.Open); tests pass a fake. version is the informational version of the app,
// such as "1.2.0+f2a4d2c…".
public sealed partial class AppInfoViewModel(Action<string> open, string engineLogPath, string? version)
{
    // The guide as GitHub shows it: the installer brings no copy of the docs, and GitHub shows
    // the Markdown readable in any browser, also for a version that is newer than this app.
    public const string GuideAddress = "https://github.com/FiekBuurman/BuurmansRgb/blob/main/docs/handleiding.md";

    // The log of the engine and the version of this app. The composition root (App) passes
    // ShellLauncher.Open, tests a fake, so no test opens Kladblok or a browser.
    public static AppInfoViewModel ForThisApp(Action<string> open) =>
        new(
            open,
            FileLog.PathOf(FileLog.EngineName),
            typeof(AppInfoViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    // Raised with the message for the status line when something could not be opened.
    public event EventHandler<string>? Failed;

    // "buurmansRgb 1.2.0": the version without the commit that the build adds after a +.
    public string VersionText { get; } = UiText.Format(Strings.AppVersion, ReadableVersion(version));

    internal static string ReadableVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? Strings.VersionUnknown : version.Split('+')[0].Trim();

    [RelayCommand]
    private void OpenLog() => Open(engineLogPath, Strings.LogOpenFailed);

    [RelayCommand]
    private void OpenGuide() => Open(GuideAddress, Strings.GuideOpenFailed);

    // As the tray menu, a log that is not there (yet), or no app for it or no browser, gives a
    // message; the details go to the log of this app.
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
