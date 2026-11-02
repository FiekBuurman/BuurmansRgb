using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Why "Knipperen" and "Tellen" in the preview are grey, in one line above its cards: the effects
// are paused, so the engine flashes and counts nothing (PreviewViewModel.CanFlash). The line names
// the reason, with "Hervatten" for the pause from the tray menu, the only one the user can end
// here; this only asks, MainViewModel sends it to the engine. It shows only while the preview has
// a device, as the buttons are on its cards.
public sealed partial class PreviewActionsHintViewModel : ObservableObject
{
    // Raised when the user asks to end the pause from the tray menu.
    public event EventHandler? ResumeRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown), nameof(CanResume), nameof(Text))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    public partial PauseReasons Reasons { get; set; }

    // Whether the preview shows a device, and so a button the line explains.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    public partial bool HasButtons { get; set; }

    public bool IsShown => Reasons != PauseReasons.None && HasButtons;

    public bool CanResume => Reasons.HasFlag(PauseReasons.Manual);

    // The same reason as the pause message at the top of the page (PauseReasonsExtensions).
    public string Text => Reasons.MostTelling() switch
    {
        PauseReasons.Manual => Strings.PreviewActionsPausedManual,
        PauseReasons.Locked => Strings.PreviewActionsPausedLocked,
        PauseReasons.Asleep => Strings.PreviewActionsPausedAsleep,
        PauseReasons.ScreenSaver => Strings.PreviewActionsPausedScreenSaver,
        PauseReasons.DisplayOff => Strings.PreviewActionsPausedDisplayOff,
        _ => string.Empty,
    };

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume() => ResumeRequested?.Invoke(this, EventArgs.Empty);
}
