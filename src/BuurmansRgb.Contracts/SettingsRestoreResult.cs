namespace BuurmansRgb.Contracts;

// FileName is the file in the zip that does not read, with Unreadable.
public sealed record SettingsRestoreResult(SettingsRestoreOutcome Outcome, string? FileName = null);
