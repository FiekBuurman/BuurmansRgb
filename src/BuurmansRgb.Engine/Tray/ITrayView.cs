namespace BuurmansRgb.Engine.Tray;

internal interface ITrayView
{
    void SetToolTip(string text);

    void SetAutoStartChecked(bool isChecked);

    void SetPauseChecked(bool isChecked);

    // The profiles to load from the menu, or null when they cannot be loaded now: the engine is
    // still starting, or the profiles could not be read. active is the scene that is on, which gets
    // a check mark; null when none is.
    void ShowProfiles(IReadOnlyList<string>? names, string? active);

    void ShowNotification(string text, TrayNotificationKind kind);
}
