using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

internal interface ICustomEffectStore
{
    // The saved own effects, or none when there is no file yet.
    IReadOnlyList<CustomEffect> Load();

    void Save(IReadOnlyList<CustomEffect> effects);

    // Writes json, a file that reads (SettingsArchive), as the file; the next Load reads it.
    void Replace(byte[] json);

    // Removes the file, as when there was none yet; the next Load reads none. For a restore that
    // takes away a file that was not there before (EngineHost).
    void Delete();
}
