using System.Diagnostics;
using System.IO;

namespace BuurmansRgb.UI.Services;

// A file the user chose in a dialog, such as a backup of the settings or a shared scene: written
// whole or not at all. An IOException or UnauthorizedAccessException goes on to the caller, who
// says what went wrong.
public static class UserFiles
{
    // Next to it first and then moved over it, so a write that fails halfway (a full disk) never
    // leaves a broken file in place of a good one the user chose to overwrite.
    public static async Task WriteReplacingAsync(string path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(bytes);
        var tempPath = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            DeleteQuietly(tempPath);
            throw;
        }
    }

    // A temporary file that is not there (its folder is gone) or cannot go stays; the error that
    // came first is the one to tell.
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Could not delete {path}: {exception.Message}");
        }
    }
}
