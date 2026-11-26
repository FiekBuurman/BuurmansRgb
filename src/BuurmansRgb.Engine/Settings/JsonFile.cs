using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace BuurmansRgb.Engine.Settings;

// Writing the engine's own JSON files, so a crash halfway never leaves a broken file behind.
internal static class JsonFile
{
    public static void Write<T>(string path, T value, JsonTypeInfo<T> typeInfo) =>
        Write(path, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo));

    // Writes to a temporary file next to path and then moves it over path.
    public static void Write(string path, byte[] json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    // Where a file that is not valid JSON goes, so the next save does not overwrite it.
    public static string BrokenPathOf(string path) => path + ".broken";

    public static void MoveAside(string path) => File.Move(path, BrokenPathOf(path), overwrite: true);

    // The content of path, or null when there is no file, it is empty JSON ("null"), or it is not
    // valid JSON: that one goes aside (and is logged) and the caller runs on without it. A file that
    // cannot be read, or cannot go aside, throws an IOException or UnauthorizedAccessException, so
    // saving never overwrites a file that is only out of reach for a moment.
    public static T? ReadOrMoveAside<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return LenientJson.Deserialize(File.ReadAllBytes(path), typeInfo);
        }
        catch (JsonException exception)
        {
            Trace.TraceError(exception.ToString());
            MoveAside(path);
            return null;
        }
    }
}
