using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

internal interface ICustomEffectStore
{
    // The saved own effects, or none when there is no file yet.
    IReadOnlyList<CustomEffect> Load();

    void Save(IReadOnlyList<CustomEffect> effects);
}
