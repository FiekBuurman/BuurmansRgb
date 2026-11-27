using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Settings;

// What SettingsArchive.Read found: the files by their name in SettingsBackup, or with Error why the
// zip is not restored (and no files).
internal sealed record SettingsArchiveContent(SettingsRestoreResult? Error, IReadOnlyDictionary<string, byte[]> Files)
{
    public static SettingsArchiveContent Refused(SettingsRestoreOutcome outcome, string? fileName = null) =>
        new(new SettingsRestoreResult(outcome, fileName), new Dictionary<string, byte[]>());
}
