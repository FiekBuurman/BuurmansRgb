namespace BuurmansRgb.Engine.Power;

internal interface IResumeEvents
{
    // Raised once per wake-up, once the lights are back, with when the pc went to sleep: null when
    // that is not known. On any thread.
    event EventHandler<DateTimeOffset?>? Restored;
}
