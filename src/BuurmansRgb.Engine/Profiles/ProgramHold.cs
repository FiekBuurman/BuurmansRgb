using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Profiles;

// ProgramWatcher.HoldUntilProgramsStop: while a program shows its scene, a time keeps its lighting
// for when the programs stop instead of showing it now. next gets the lighting that would come back
// with the scene that is on with it, and gives what comes back instead. True when it was kept.
internal delegate bool ProgramHold(Func<HeldLighting, HeldLighting> next);

// Lighting with the scene that is on with it, or null for none.
internal readonly record struct HeldLighting(LightingState Lighting, string? Scene);
