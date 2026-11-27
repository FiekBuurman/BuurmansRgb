using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

// Where CustomEffectLibrary gives its effects to: all it needs of the controller.
internal interface ICustomEffectSink
{
    void SetCustomEffects(IReadOnlyList<CustomEffect> effects);
}
