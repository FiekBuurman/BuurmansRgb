using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// The zip of "Instellingen bewaren…" and "Instellingen terugzetten…" (SettingsBackup): making one
// from the files of the engine, and reading one back with every check before anything is replaced.
// A zip from elsewhere is not trusted: only the three names of SettingsBackup at the top (no folder,
// no path, so nothing can land outside the folder of the engine), each once, not too large packed
// or unpacked (read in steps up to the limit, not by the size the zip claims), and each a file the
// stores of the engine can read. Without the stores: they read the files again once replaced.
internal static class SettingsArchive
{
    private const int ReadChunkBytes = 81_920;

    // The names in SettingsBackup.FileNames that files has, in that order; the others are left out.
    public static byte[] Create(IReadOnlyDictionary<string, byte[]> files)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in SettingsBackup.FileNames)
            {
                if (files.TryGetValue(name, out var json))
                {
                    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    using var stream = entry.Open();
                    stream.Write(json);
                }
            }
        }

        return buffer.ToArray();
    }

    // The files in archive by their name in SettingsBackup, or why it is not a backup to restore.
    public static SettingsArchiveContent Read(byte[] archive)
    {
        if (archive.Length > SettingsBackup.MaxArchiveBytes)
        {
            return SettingsArchiveContent.Refused(SettingsRestoreOutcome.TooLarge);
        }

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            using var zip = new ZipArchive(new MemoryStream(archive, writable: false), ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                var name = KnownName(entry.FullName);
                if (name is null || files.ContainsKey(name))
                {
                    Trace.TraceWarning($"Restoring settings: the zip has '{entry.FullName}', which is not one of the files of a backup.");
                    return SettingsArchiveContent.Refused(SettingsRestoreOutcome.NotABackup);
                }

                if (entry.Length > SettingsBackup.MaxFileBytes || ReadAtMost(entry, SettingsBackup.MaxFileBytes) is not { } json)
                {
                    Trace.TraceWarning($"Restoring settings: '{name}' in the zip is larger than {SettingsBackup.MaxFileBytes} bytes.");
                    return SettingsArchiveContent.Refused(SettingsRestoreOutcome.TooLarge);
                }

                // An editor may have put one in front; the stores read the file without it.
                files.Add(name, WithoutByteOrderMark(json).ToArray());
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or IOException)
        {
            // Not a zip, a damaged one, or one with encryption or a compression .NET does not read.
            Trace.TraceWarning($"Restoring settings: not a zip that can be read: {exception.Message}");
            return SettingsArchiveContent.Refused(SettingsRestoreOutcome.NotABackup);
        }

        if (files.Count == 0)
        {
            Trace.TraceWarning("Restoring settings: the zip has none of the files of a backup.");
            return SettingsArchiveContent.Refused(SettingsRestoreOutcome.NotABackup);
        }

        foreach (var (name, json) in files)
        {
            if (!IsReadable(name, json))
            {
                return SettingsArchiveContent.Refused(SettingsRestoreOutcome.Unreadable, name);
            }
        }

        return new SettingsArchiveContent(null, files);
    }

    // settings.json from a backup with what belongs to this pc instead of the pc it came from:
    // whether the user chose autostart here (autoStartChosen, which the Run key of this pc goes
    // with, and which --first-run of the next update reads) and whether this pc had real devices
    // (hadRealDevices). The rest stays as the backup has it, also keys this engine does not know.
    // Only for a file Read accepted.
    public static byte[] WithThisPc(byte[] settingsJson, bool? autoStartChosen, bool hadRealDevices)
    {
        var root = JsonNode.Parse(WithoutByteOrderMark(settingsJson))!.AsObject();
        Set(root, "autoStartChosen", autoStartChosen is { } chosen ? JsonValue.Create(chosen) : null);
        // Only in the file once it is true, as the engine writes it (EngineSettings.HadRealDevices).
        Set(root, "hadRealDevices", hadRealDevices ? JsonValue.Create(true) : null);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            root.WriteTo(writer);
        }

        return buffer.ToArray();
    }

    // Also the name in another case, as Windows makes no difference; the file gets the name of
    // SettingsBackup. A name with a folder or drive in front is no name of a backup.
    private static string? KnownName(string fullName) =>
        SettingsBackup.FileNames.FirstOrDefault(name => string.Equals(name, fullName, StringComparison.OrdinalIgnoreCase));

    // Null when the entry unpacks to more than maxBytes, whatever its header says.
    private static byte[]? ReadAtMost(ZipArchiveEntry entry, int maxBytes)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[ReadChunkBytes];
        int read;
        while ((read = stream.Read(chunk)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    // As the stores read them (LenientJson): one value of the wrong type is left out, but a file
    // that is not JSON, not an object, or "null" is refused.
    private static bool IsReadable(string name, byte[] json)
    {
        try
        {
            object? file = name switch
            {
                SettingsBackup.SettingsFileName => LenientJson.Deserialize(json, EngineSettingsJsonContext.Default.EngineSettings),
                SettingsBackup.ProfilesFileName => LenientJson.Deserialize(json, EngineSettingsJsonContext.Default.ProfilesFile),
                _ => LenientJson.Deserialize(json, EngineSettingsJsonContext.Default.CustomEffectsFile),
            };
            if (file is null)
            {
                Trace.TraceWarning($"Restoring settings: '{name}' in the zip is empty (null).");
                return false;
            }

            // The engine writes this pc into it (WithThisPc), which reads it as a tree: a name twice
            // in one object, which the stores take the last of, is refused there.
            if (name == SettingsBackup.SettingsFileName)
            {
                WithThisPc(json, autoStartChosen: null, hadRealDevices: false);
            }

            return true;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            Trace.TraceWarning($"Restoring settings: '{name}' in the zip does not read: {exception.Message}");
            return false;
        }
    }

    private static void Set(JsonObject root, string name, JsonNode? value)
    {
        foreach (var key in root.Select(pair => pair.Key).Where(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            root.Remove(key);
        }

        if (value is not null)
        {
            root[name] = value;
        }
    }

    private static ReadOnlySpan<byte> WithoutByteOrderMark(byte[] json) =>
        json.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? json.AsSpan(3) : json;
}
