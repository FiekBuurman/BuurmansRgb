using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// A short blink over what a device shows, so the user can see which device in the list is which
// one in the pc: bright white and off in turn, Blinks times, then the device's own lighting again.
// It runs on the engine time, so it ends on time whatever the speed of the device's effect.
internal sealed class DeviceFlash(TimeSpan start)
{
    public const int Blinks = 3;

    // White for half a blink, off for the other half.
    public static TimeSpan BlinkDuration { get; } = TimeSpan.FromSeconds(0.5);

    public static TimeSpan Duration { get; } = BlinkDuration * Blinks;

    public bool IsOver(TimeSpan time) => time - start >= Duration;

    public RgbColor ColorAt(TimeSpan time)
    {
        var halfBlinks = (long)((time - start) / (BlinkDuration / 2));
        return halfBlinks % 2 == 0 ? RgbColor.White : RgbColor.Black;
    }
}
