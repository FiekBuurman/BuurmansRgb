using System.ComponentModel;
using System.IO;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.Services;

// The programs that show a window now, for "Kies uit wat nu draait": a game or app the user has
// open, without the background processes and the parts of Windows nobody would pick. Each one by
// the name a user knows it by: the description in its exe file, else the title of its window, else
// its exe name; the field still gets the exe name. In the order of those names.
public static class WindowedPrograms
{
    // A longer title, such as that of a document, is cut off, so the list does not get that wide.
    internal const int MaxNameLength = 60;

    // Parts of Windows itself that have a window, often an invisible one, without being an app a
    // user opens: the frame around Store apps (which stands for any Store app, not for one), the
    // touch keyboard and emoji panel, the start menu, search and the lock screen. Windows that a
    // user does open, such as Instellingen (SystemSettings.exe) and Verkenner (explorer.exe), stay
    // in the list. Typed in the field, these still work as before.
    internal static IReadOnlySet<string> WindowsParts { get; } = new HashSet<string>(ProgramNames.Comparer)
    {
        "ApplicationFrameHost.exe",
        "TextInputHost.exe",
        "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe",
        "SearchHost.exe",
        "SearchApp.exe",
        ProgramNames.LockScreen,
    };

    public static IReadOnlyList<RunningProgram> Find() => Find(SystemProcesses.Windowed(), SystemProcesses.Description);

    // describe gives the description in the exe file of a process by its id; when it throws, as it
    // does for a process that ended or cannot be read, the title or the exe name stands instead.
    internal static IReadOnlyList<RunningProgram> Find(IEnumerable<WindowedProcess> processes, Func<int, string?> describe)
    {
        var programs = new List<RunningProgram>();
        var byExeName = processes
            .Select(process => (Process: process, ExeName: ProgramNames.Normalize(process.Name)))
            .Where(found => found.ExeName is not null && !WindowsParts.Contains(found.ExeName))
            .GroupBy(found => found.ExeName!, found => found.Process, ProgramNames.Comparer);
        foreach (var program in byExeName)
        {
            // One process of the program that describes it is enough; the others are not read.
            var name = program.Select(process => Describe(process.Id, describe)).FirstOrDefault(IsName)
                ?? program.Select(process => process.Title).FirstOrDefault(IsName);
            programs.Add(new RunningProgram(program.Key, Readable(name, program.Key)));
        }

        return [.. programs
            .OrderBy(program => program.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(program => program.ExeName, ProgramNames.Comparer)];
    }

    private static string? Describe(int processId, Func<int, string?> describe)
    {
        try
        {
            return describe(processId);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Ended, of another user, protected, or its file gone: no description then.
            return null;
        }
    }

    private static bool IsName(string? name) => !string.IsNullOrWhiteSpace(name);

    // The name, trimmed and cut off when it is long; the exe name itself when there is none, or
    // when the name only says the exe name again ("notepad" for notepad.exe).
    private static string Readable(string? name, string exeName)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name)
            || ProgramNames.Comparer.Equals(name, exeName)
            || ProgramNames.Comparer.Equals(name, Path.GetFileNameWithoutExtension(exeName)))
        {
            return exeName;
        }

        return name.Length <= MaxNameLength ? name : string.Concat(name.AsSpan(0, MaxNameLength - 1).TrimEnd(), "…");
    }
}
