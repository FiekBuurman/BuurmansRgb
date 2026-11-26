namespace BuurmansRgb.Contracts.Testing;

// Backing up and restoring the settings, and sharing a scene or an effect.
public sealed partial class FakeEngineApi
{
    // What "Instellingen bewaren…" gets, and what "terugzetten…" answers; the archives it got.
    public byte[] ExportedSettings { get; set; } = [1, 2, 3];

    public SettingsRestoreResult RestoreResult { get; set; } = new(SettingsRestoreOutcome.Restored);

    public List<byte[]> RestoredArchives { get; } = [];

    public async Task<byte[]> ExportSettingsAsync(CancellationToken ct = default)
    {
        await Record("export settings");
        return ExportedSettings;
    }

    public async Task<SettingsRestoreResult> RestoreSettingsAsync(byte[] archive, CancellationToken ct = default)
    {
        await Record("restore settings");
        RestoredArchives.Add(archive);
        return RestoreResult;
    }

    // What "Exporteren…" and "Code kopiëren" get, and what an import answers; the JSON it got.
    public byte[] ExportedShare { get; set; } = "{\"version\":1}"u8.ToArray();

    public ShareImportResult ImportResult { get; set; } = new(ShareImportOutcome.Imported, SharedKind.Scene, "Gamen", "Gamen");

    public List<byte[]> ImportedShares { get; } = [];

    public async Task<byte[]> ExportSceneAsync(string name, CancellationToken ct = default)
    {
        await Record($"export scene {name}");
        return ExportedShare;
    }

    public async Task<byte[]> ExportCustomEffectAsync(string id, CancellationToken ct = default)
    {
        await Record($"export effect {id}");
        return ExportedShare;
    }

    public async Task<ShareImportResult> ImportSharedAsync(byte[] json, CancellationToken ct = default)
    {
        await Record("import");
        ImportedShares.Add(json);
        if (ImportResult is { Outcome: ShareImportOutcome.Imported, Kind: SharedKind.Scene, Name: { } name } && !Profiles.Contains(name))
        {
            Profiles.Add(name);
            Profiles.Sort(StringComparer.CurrentCultureIgnoreCase);
        }

        return ImportResult;
    }
}
