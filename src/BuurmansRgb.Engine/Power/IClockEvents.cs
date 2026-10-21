namespace BuurmansRgb.Engine.Power;

internal interface IClockEvents
{
    // Raised when the clock may have jumped: the time or the time zone was changed, or Windows
    // woke up from sleep or hibernation. On any thread.
    event EventHandler? Changed;
}
