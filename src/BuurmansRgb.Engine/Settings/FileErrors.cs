namespace BuurmansRgb.Engine.Settings;

internal static class FileErrors
{
    // What the file system throws when a file is out of reach, or not for us to change, now.
    public static bool IsAccessError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;
}
