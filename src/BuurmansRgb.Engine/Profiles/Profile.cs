using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Profiles;

// All the lighting, saved under a name to go back to later, and when the engine loads it by itself.
internal sealed record Profile(string Name, LightingState Lighting)
{
    public ProfileAutomation Automation { get; init; } = ProfileAutomation.None;
}
