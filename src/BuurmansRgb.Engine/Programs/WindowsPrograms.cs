using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Programs;

// Parts of Windows by their exe name, as ProcessImageName reads it, for what follows the foreground
// window (SystemForegroundWindow, SystemScreenSaverEvents).
internal static class WindowsPrograms
{
    // The lock screen, also the sign-in screen after a screen saver with "aanmeldingsscherm bij
    // hervatten".
    public const string LockScreen = "LockApp.exe";

    private const string ScreenSaverExtension = ".scr";

    public static bool IsLockScreen(string? program) => program is not null && ProgramNames.Comparer.Equals(program, LockScreen);

    // Whether program is a screen saver, by its exe name (any case).
    public static bool IsScreenSaver(string? program) =>
        program is not null
        && program.Length > ScreenSaverExtension.Length
        && program.EndsWith(ScreenSaverExtension, StringComparison.OrdinalIgnoreCase);
}
