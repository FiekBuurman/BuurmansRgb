namespace BuurmansRgb.Engine.Tray;

internal interface ITrayView
{
    void SetToolTip(string text);

    void SetAutoStartChecked(bool isChecked);

    void ShowNotification(string text, TrayNotificationKind kind);
}
