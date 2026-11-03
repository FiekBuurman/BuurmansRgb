using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

internal interface ICustomEffectStore
{
    // The saved own effects, or none when there is no file yet.
    IReadOnlyList<CustomEffect> Load();

    void Save(IReadOnlyList<CustomEffect> effects);

    // Writes json, a file that reads (SettingsArchive), as the file; the next Load reads it.
    void Replace(byte[] json);
}
