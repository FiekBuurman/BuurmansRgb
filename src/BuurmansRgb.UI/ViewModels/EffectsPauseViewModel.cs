using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Whether the engine has paused the effects, and why. Only the pause from the tray menu can end
// here; the others end with Windows. This only asks; MainViewModel sends it to the engine.
// When a pause ends, the message says so for ResumedMessageTime instead of vanishing at once, so a
// screen reader reads it (it only reads what shows) and the user sees the click worked.
public sealed partial class EffectsPauseViewModel : ObservableObject, IDisposable
{
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private ITimer? _resumedTimer;

    // time runs the message after a pause; tests pass a fake one.
    public EffectsPauseViewModel(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    // Raised when the user asks to end the pause from the tray menu.
    public event EventHandler? ResumeRequested;

    public static TimeSpan ResumedMessageTime { get; } = TimeSpan.FromSeconds(5);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaused), nameof(CanResume), nameof(Text), nameof(IsShown))]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    public partial PauseReasons Reasons { get; private set; }

    // True for a moment after a pause ended.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Text), nameof(IsShown))]
    public partial bool HasResumed { get; private set; }

    public bool IsPaused => Reasons != PauseReasons.None;

    public bool IsShown => IsPaused || HasResumed;

    public bool CanResume => Reasons.HasFlag(PauseReasons.Manual);

    // The pause from the tray menu first: that is the one the user can end. While Windows is locked
    // and asleep, it wakes up to the lock screen, so unlocking is what brings the effects back. The
    // screen goes on before that, so a screen saver comes next and a screen that is off last: the
    // screen may go off while the screen saver runs, and the same touch ends both.
    public string Text =>
        CanResume ? Strings.EffectsPausedManual
        : Reasons.HasFlag(PauseReasons.Locked) ? Strings.EffectsPausedLocked
        : Reasons.HasFlag(PauseReasons.Asleep) ? Strings.EffectsPausedAsleep
        : Reasons.HasFlag(PauseReasons.ScreenSaver) ? Strings.EffectsPausedScreenSaver
        : Reasons.HasFlag(PauseReasons.DisplayOff) ? Strings.EffectsPausedDisplayOff
        : HasResumed ? Strings.EffectsResumed
        : string.Empty;

    // HasResumed changes first, so the message goes straight from the pause to its end without
    // disappearing in between.
    public void Show(PauseReasons reasons)
    {
        if (reasons == Reasons)
        {
            return;
        }

        var resumed = reasons == PauseReasons.None;
        HasResumed = resumed;
        Reasons = reasons;
        if (resumed)
        {
            _resumedTimer ??= _time.CreateTimer(_ => OnResumedTimer(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _resumedTimer.Change(ResumedMessageTime, Timeout.InfiniteTimeSpan);
        }
        else
        {
            StopTimer();
        }
    }

    // Without the engine nothing is known about a pause, also not that one ended.
    public void Clear()
    {
        StopTimer();
        HasResumed = false;
        Reasons = PauseReasons.None;
    }

    public void Dispose() => _resumedTimer?.Dispose();

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume() => ResumeRequested?.Invoke(this, EventArgs.Empty);

    // The timer runs on the thread pool; the message belongs to the UI thread.
    private void OnResumedTimer()
    {
        if (_uiContext is null)
        {
            HasResumed = false;
        }
        else
        {
            _uiContext.Post(_ => HasResumed = false, state: null);
        }
    }

    private void StopTimer() => _resumedTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
}
