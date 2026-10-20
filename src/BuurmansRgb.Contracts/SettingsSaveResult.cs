namespace BuurmansRgb.Contracts;

// Succeeded is false when the engine could not write its settings file; the lights keep working,
// but the change is lost after a restart.
public sealed record SettingsSaveResult(bool Succeeded);
