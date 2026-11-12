using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Profiles;

// ProgramWatcher.HoldUntilProgramsStop: while a program shows its scene, a time keeps its lighting
// for when the programs stop instead of showing it now. lighting gives that from the lighting that
// would come back; scene is the scene that is on with it, or null. True when it was kept.
internal delegate bool ProgramHold(Func<LightingState, LightingState> lighting, string? scene);
