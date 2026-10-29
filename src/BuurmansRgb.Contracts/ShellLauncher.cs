using System.Diagnostics;

namespace BuurmansRgb.Contracts;

// Opens a file or a web address as Verkenner would: a .log in Kladblok or the app that opens .log
// files, a link in the browser. Throws Win32Exception when Windows cannot open it, such as a file
// that is not there. Shared by "Logbestand openen" in the tray menu and in the settings app.
public static class ShellLauncher
{
    public static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
}
