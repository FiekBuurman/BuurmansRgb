namespace BuurmansRgb.Contracts;

// The rules for the program that loads a profile when it starts: the file name of its .exe, such
// as "eldenring.exe". Shared so the UI can check a name before it asks.
public static class ProgramNames
{
    public const int MaxLength = 100;

    // The lock screen of Windows, also the sign-in screen after a screen saver with
    // "aanmeldingsscherm bij hervatten"; one of the ShellParts.
    public const string LockScreen = "LockApp.exe";

    private const string Extension = ".exe";

    // What a user may pick instead of the program itself; the process that runs is another one.
    private static readonly string[] ShortcutsAndScripts = [".lnk", ".url", ".bat", ".cmd"];

    // Windows does not tell file names apart by case.
    public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;

    // Parts of Windows that show over a program without taking the user away from it: the lock
    // screen, the Start menu and search (SearchHost.exe on Windows 11, SearchApp.exe on Windows 10),
    // and the panes for notifications and quick settings (ShellExperienceHost.exe, and ShellHost.exe
    // since Windows 11 24H2). The engine does not let them count as a program in the foreground, and
    // "Kies uit wat nu draait" leaves them out. After Comparer, which it needs.
    public static IReadOnlySet<string> ShellParts { get; } = new HashSet<string>(Comparer)
    {
        LockScreen, "StartMenuExperienceHost.exe", "SearchHost.exe", "SearchApp.exe", "ShellExperienceHost.exe", "ShellHost.exe",
    };

    // The file name with ".exe", or null when it is empty, too long, a shortcut or script, or holds
    // a character that a file name cannot have. A path gives its file name, and a name without
    // ".exe" gets it, so "C:\Games\eldenring.exe" and "eldenring" both give "eldenring.exe".
    public static string? Normalize(string? program)
    {
        var name = FileName(program);
        if (string.IsNullOrEmpty(name) || IsShortcutOrScript(name))
        {
            return null;
        }

        if (!name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            name += Extension;
        }

        return name.Length > Extension.Length && name.Length <= MaxLength && !name.Any(IsInvalid) ? name : null;
    }

    // A shortcut or script (.lnk, .url, .bat, .cmd), such as a game's shortcut on the desktop.
    public static bool IsShortcutOrScript(string? program) =>
        FileName(program) is { } name && ShortcutsAndScripts.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    private static string? FileName(string? program)
    {
        var name = program?.Trim().Trim('"');
        return string.IsNullOrEmpty(name) ? null : name[(name.LastIndexOfAny(['\\', '/']) + 1)..].Trim();
    }

    private static bool IsInvalid(char character) => char.IsControl(character) || ":*?\"<>|".Contains(character);
}
