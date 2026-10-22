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

    public static bool IsAccessError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;
}
