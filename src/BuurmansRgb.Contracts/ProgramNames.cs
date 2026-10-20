namespace BuurmansRgb.Contracts;

// The rules for the program that loads a profile when it starts: the file name of its .exe, such
// as "eldenring.exe". Shared so the UI can check a name before it asks.
public static class ProgramNames
{
    public const int MaxLength = 100;

    private const string Extension = ".exe";

    // Windows does not tell file names apart by case.
    public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;

    // The file name with ".exe", or null when it is empty, too long or holds a character that a
    // file name cannot have. A path gives its file name, and a name without ".exe" gets it, so
    // "C:\Games\eldenring.exe" and "eldenring" both give "eldenring.exe".
    public static string? Normalize(string? program)
    {
        var name = program?.Trim().Trim('"');
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        name = name[(name.LastIndexOfAny(['\\', '/']) + 1)..].Trim();
        if (!name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            name += Extension;
        }

        return name.Length > Extension.Length && name.Length <= MaxLength && !name.Any(IsInvalid) ? name : null;
    }

    private static bool IsInvalid(char character) => char.IsControl(character) || ":*?\"<>|".Contains(character);
}
