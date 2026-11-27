using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// A change with fade: the devices go over from the colors they show to the new ones (SceneFade).
internal sealed partial class LightingController
{
    // True while a device fades over to a new lighting.
    public bool IsFading
    {
        get
        {
            lock (_gate)
            {
                return _outputs.All.Any(output => output.IsFading);
            }
        }
    }

    // Call it once the new lighting is in, before the RenderLocked that shows it. Lets every
    // device whose lighting, per-LED colors or layer differ from before go over from the colors
    // it shows now, also in the middle of a fade, to what the next RenderLocked gives it, in
    // SceneFade. A device that stays the same goes on as it was: an effect that moves does not
    // stand still for a moment, and a fade that runs goes on, on time. At once, so a fade that
    // runs ends, with a fade of zero, before there was lighting (a null before), and during a
    // pause: no frames run then, and what goes to the devices, such as a scene on time while
    // Windows is locked, does not wait. Show has not given the new effects yet, so BeginFade still
    // renders the old ones.
    private void FadeLocked(LightingState? before)
    {
        var fade = _disposed || _pause != PauseReasons.None || before is null ? TimeSpan.Zero : _sceneFade;
        var time = EffectTime;
        foreach (var output in _outputs.All)
        {
            if (fade <= TimeSpan.Zero)
            {
                output.EndFade();
            }
            else if (ChangedLocked(output.Device, before!))
            {
                output.BeginFade(time, fade);
            }
        }
    }

    // Whether device shows other lighting, other per-LED colors or another layer now than in
    // before. Only the per-LED effect shows those colors, so they count only for a device that
    // shows it, before and now (with another effect before or now, the lighting differs already);
    // the layer shows with any effect, but not on a device that is off, before and now.
    private bool ChangedLocked(RgbDevice device, LightingState before)
    {
        var lighting = _lighting.Get(device.Id);
        return before.Devices.GetValueOrDefault(device.Id, before.Global) != lighting
            || (lighting.EffectId == EffectIds.PerLed && !_ledColors.SameAs(before.LedColors, device))
            || (lighting.On && !_overlays.SameAs(before.Overlays, device));
    }

    // A change of its own, without fade, shows at once: it ends the fade of the device it changes,
    // or of every device for a null deviceId (a global change, or all lighting at once). The
    // RenderLocked that follows sends the new lighting, and stops the render loop when nothing
    // else moves.
    private void EndFadesLocked(string? deviceId = null)
    {
        if (deviceId is not null)
        {
            _outputs.Find(deviceId)?.EndFade();
            return;
        }

        foreach (var output in _outputs.All)
        {
            output.EndFade();
        }
    }

    // Gives every device that fades the end of its fade at once, as when a pause starts or the
    // engine stops, so no device stays halfway and nothing waits for the fade. Returns true when
    // there was one.
    private bool StopFadesLocked() => StopLocked(static output => output.IsFading, static output => output.EndFade());
}
