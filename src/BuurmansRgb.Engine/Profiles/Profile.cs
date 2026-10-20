using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Profiles;

// All the lighting, saved under a name to go back to later.
internal sealed record Profile(string Name, LightingState Lighting);
