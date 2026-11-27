using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Profiles;

// Lighting with the scene that is on with it, or null for none.
internal readonly record struct HeldLighting(LightingState Lighting, string? Scene);
