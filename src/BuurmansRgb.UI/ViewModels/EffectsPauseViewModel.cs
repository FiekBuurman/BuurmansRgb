using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Whether the engine has paused the effects, and why. Only the pause from the tray menu can end
// here; the others end with Windows. This only asks; MainViewModel sends it to the engine.
public sealed partial class EffectsPauseViewModel : ObservableObject
{
    // Raised when the user asks to end the pause from the tray menu.
    public event EventHandler? ResumeRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaused), nameof(CanResume), nameof(Text))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    public partial PauseReasons Reasons { get; private set; }

    public bool IsPaused => Reasons != PauseReasons.None;

    public bool CanResume => Reasons.HasFlag(PauseReasons.Manual);

    // The pause from the tray menu first: that is the one the user can end. While Windows is locked
    // and asleep, it wakes up to the lock screen, so unlocking is what brings the effects back.
    public string Text =>
        CanResume ? Strings.EffectsPausedManual
        : Reasons.HasFlag(PauseReasons.Locked) ? Strings.EffectsPausedLocked
        : Reasons.HasFlag(PauseReasons.Asleep) ? Strings.EffectsPausedAsleep
        : string.Empty;

    public void Show(PauseReasons reasons) => Reasons = reasons;

    // Without the engine nothing is known about a pause.
    public void Clear() => Reasons = PauseReasons.None;

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume() => ResumeRequested?.Invoke(this, EventArgs.Empty);
}
