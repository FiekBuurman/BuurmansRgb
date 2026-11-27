using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

// The time an animated effect sees: the engine time, run at the device's speed. A new speed only
// counts from the next tick on, so the effect carries on from where it is instead of jumping.
// Devices that get a new speed at the same moment, such as all devices at once, stay in step.
internal sealed class EffectClock
{
    private double _runningSpeed = EffectSpeed.Default;
    private TimeSpan? _lastTime;
    private TimeSpan _effectTime;

    // The speed from the next tick on.
    public double Speed { get; set; } = EffectSpeed.Default;

    // The first tick starts at time times the speed, so a device at the normal speed runs on the
    // engine time itself, in step with every other device at the normal speed.
    public TimeSpan Tick(TimeSpan time)
    {
        _effectTime = _lastTime is { } last ? _effectTime + (time - last) * _runningSpeed : time * Speed;
        _lastTime = time;
        _runningSpeed = Speed;
        return _effectTime;
    }
}
