using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// Which reason for a pause to tell when there is more than one: the pause message at the top of
// the page (EffectsPauseViewModel) and the line above the preview (PreviewActionsHintViewModel)
// name the same one. The pause from the tray menu first: that is the one the user can end. While
// Windows is locked and asleep, it wakes up to the lock screen, so unlocking is what brings the
// effects back. The screen goes on before that, so a screen saver comes next and a screen that is
// off last: the screen may go off while the screen saver runs, and the same touch ends both.
internal static class PauseReasonsExtensions
{
    // The one reason that matters most, or None while the effects run.
    public static PauseReasons MostTelling(this PauseReasons reasons) =>
        reasons.HasFlag(PauseReasons.Manual) ? PauseReasons.Manual
        : reasons.HasFlag(PauseReasons.Locked) ? PauseReasons.Locked
        : reasons.HasFlag(PauseReasons.Asleep) ? PauseReasons.Asleep
        : reasons.HasFlag(PauseReasons.ScreenSaver) ? PauseReasons.ScreenSaver
        : reasons.HasFlag(PauseReasons.DisplayOff) ? PauseReasons.DisplayOff
        : PauseReasons.None;
}
