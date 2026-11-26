using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// A device or a zone that blinks for a moment, so the user can find it in the pc.
internal sealed partial class LightingController
{
    // Lets the device blink for a moment (DeviceFlash), so the user can find it in the pc; then it
    // shows its own lighting again. Nothing is stored and LightingChanged is not raised. The render
    // loop runs only until the flash is over. One device flashes at a time, so the user who clicks
    // through the list sees only the last one; that also holds for FlashZone. While paused the
    // devices stay as they are, so nothing happens then. A flash changes no lighting, so a device
    // that is gone (the list in the settings app may be behind) is skipped instead of reported as
    // an error.
    public void Flash(string deviceId) => StartFlash(deviceId, zoneIndex: null);

    // Flash for the LEDs of one zone (an index into the device's zones) only, so the user sees
    // which header of a motherboard is which fan or strip; the rest of the device keeps its
    // colors. It replaces a flash of any device or zone, as Flash does. The zone counts as the
    // engine knows it: a zone the list in the settings app has but the engine not (any more) is
    // skipped like a device that is gone. A zone without LEDs, like a device without, has nothing
    // to blink, but still ends the flash before.
    public void FlashZone(string deviceId, int zoneIndex) => StartFlash(deviceId, zoneIndex);

    // Flash and FlashZone: a null zoneIndex flashes the whole device. Without LEDs to blink, of
    // the zone or the device, no flash starts, but the request still replaces the one before, as
    // it did before zones could flash: that one ends with the own colors.
    private void StartFlash(string deviceId, int? zoneIndex)
    {
        var rendered = false;
        lock (_gate)
        {
            if (_outputs.Find(deviceId) is not { } output || _pause != PauseReasons.None || !_lighting.IsSet)
            {
                return;
            }

            var (first, count) = (0, output.Buffer.Length);
            if (zoneIndex is { } index)
            {
                if (!output.Device.TryGetZone(index, out _))
                {
                    return;
                }

                (first, count) = RgbZone.RangeOf(output.Device.Zones, output.Device.LedCount, index);
            }

            if (count == 0)
            {
                rendered = StopFlashesLocked();
            }
            else
            {
                StopFlashesLocked(except: output);
                var time = EffectTime;
                output.StartFlash(time, first, count);
                output.Render(time);
                output.Send();
                _sequence++;
                rendered = true;
            }

            UpdateAnimationLocked();
        }

        Publish(rendered);
    }

    // Gives every device that is flashing, but except, its own colors again. Returns true when
    // there was one.
    private bool StopFlashesLocked(DeviceOutput? except = null) =>
        StopLocked(static output => output.IsFlashing, static output => output.StopFlash(), except);
}
