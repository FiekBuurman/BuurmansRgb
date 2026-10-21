using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Power;

// Whether nobody is at the pc: Windows is locked, the session is on no screen or the pc sleeps.
// The same with the pause on lock and sleep on or off (AutoPause). A screen that is off does not
// count: somebody may still be at the pc, listening to music.
internal interface IAwayState
{
    // Locked and Asleep, or None while somebody is at the pc.
    PauseReasons Away { get; }

    // Raised when Away got another value, with the new one, in order. On any thread.
    event EventHandler<PauseReasons>? AwayChanged;
}
