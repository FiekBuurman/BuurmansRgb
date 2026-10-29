using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The bottom of Instellingen: "Logbestand openen", as in the tray menu, and the version of
// buurmansRgb, with a link to the guide once there is one. The app opens the log of the engine
// itself, where the engine writes it (FileLog.PathOf), instead of asking the engine: so it also
// works when the engine does not run or does not answer, which is when the log is needed most,
// and the editor comes to the front, as this app has the focus. open opens a file or web address
// as Verkenner does (ShellLauncher.Open); tests pass a fake. version is the informational version
// of the app, such as "1.2.0+f2a4d2c…". guideAddress is where "Handleiding" opens the guide; without
// one the link is hidden.
public sealed partial class AppInfoViewModel(Action<string> open, string engineLogPath, string? version, string? guideAddress = null)
{
    // Where the guide is for every user of this app: none yet, so only the version shows. The
    // repository on GitHub is private, so a link to it would give a login page or a 404. An
    // address here (a web address, or the path of a guide the installer brings) shows the link.
    public const string? GuideAddress = null;

    // The log of the engine, the version of this app and the guide (GuideAddress). The composition
    // root (App) passes ShellLauncher.Open, tests a fake, so no test opens Kladblok or a browser.
    public static AppInfoViewModel ForThisApp(Action<string> open, string? guideAddress = GuideAddress) =>
        new(
            open,
            FileLog.PathOf(FileLog.EngineName),
            typeof(AppInfoViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            guideAddress);

    // Raised with the message for the status line when something could not be opened.
    public event EventHandler<string>? Failed;

    // "buurmansRgb 1.2.0": the version without the commit that the build adds after a +.
    public string VersionText { get; } = UiText.Format(Strings.AppVersion, ReadableVersion(version));

    // Whether "Handleiding" shows: only with an address to open.
    public bool HasGuide { get; } = !string.IsNullOrWhiteSpace(guideAddress);

    internal static string ReadableVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? Strings.VersionUnknown : version.Split('+')[0].Trim();

    [RelayCommand]
    private void OpenLog() => Open(engineLogPath, Strings.LogOpenFailed);

    [RelayCommand(CanExecute = nameof(HasGuide))]
    private void OpenGuide()
    {
        if (HasGuide && guideAddress is { } address)
        {
            Open(address, Strings.GuideOpenFailed);
        }
    }

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
