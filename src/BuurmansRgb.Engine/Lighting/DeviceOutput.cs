using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// One device with the effect it shows and its LED buffers. The buffers are made once per device
// list, so rendering a frame allocates nothing. The clock is passed on when the device list is
// made again, so an animation keeps running where it was. Only used under the lock of
// LightingController.
internal sealed class DeviceOutput(IDeviceProvider provider, RgbDevice device, EffectClock? clock = null)
{
    // What the device got last, so a frame that changes nothing is not sent.
    private readonly RgbColor[] _sent = new RgbColor[device.LedCount];

    // The LEDs of each zone that has any, and those after the last zone, which belong to none;
    // with zones alike they all show the image of the longest, which comes first.
    private readonly (int FirstLed, int LedCount)[] _parts = PartsOf(device);

    // Until the first Show; LightingController shows the lighting before it renders.
    private IEffect _effect = new StaticEffect(DeviceLighting.Default.Color);
    private double _brightness = DeviceLighting.Default.Brightness;
    private bool _zonesAlike;
    private DeviceFlash? _flash;

    public IDeviceProvider Provider { get; } = provider;

    public RgbDevice Device { get; } = device;

    public RgbColor[] Buffer { get; } = new RgbColor[device.LedCount];

    public EffectClock Clock { get; } = clock ?? new();

    // A device that flashes needs frames until the flash is over, whatever its effect.
    public bool IsAnimated => _effect.IsAnimated || _flash is not null;

    public bool IsFlashing => _flash is not null;

    // Brightness runs from 0 to 1 and applies to whatever the effect renders; speed counts from
    // the next Render on. With zonesAlike the effect renders once, over the longest zone, and
    // every other zone gets that image stretched to its own length, so a mouse with two zones of
    // one LED shows one color in a rainbow. A device with fewer than two zones with LEDs looks the
    // same either way.
    public void Show(IEffect effect, double brightness, double speed, bool zonesAlike = false)
    {
        _effect = effect;
        _brightness = brightness;
        _zonesAlike = zonesAlike;
        Clock.Speed = speed;
    }

    // From time on, the renders show the flash over the effect on the LEDs firstLed to
    // firstLed + ledCount, such as one zone; the other LEDs keep showing the effect, and the
    // lighting itself stays.
    public void StartFlash(TimeSpan time, int firstLed, int ledCount) => _flash = new DeviceFlash(time, firstLed, ledCount);

    public void StopFlash() => _flash = null;

    // time is the engine time; the effect gets it at the device's speed. A flash goes over the
    // brightness, so a dimmed device still blinks bright; the first render after it shows the
    // effect again.
    public void Render(TimeSpan time)
    {
        var leds = Buffer.AsSpan();
        var effectTime = Clock.Tick(time);
        if (_zonesAlike && _parts.Length > 1)
        {
            var image = leds.Slice(_parts[0].FirstLed, _parts[0].LedCount);
            _effect.Render(image, effectTime);
            for (var part = 1; part < _parts.Length; part++)
            {
                Stretch(image, leds.Slice(_parts[part].FirstLed, _parts[part].LedCount));
            }
        }
        else
        {
            _effect.Render(leds, effectTime);
        }

        if (_brightness < 1.0)
        {
            foreach (ref var led in leds)
            {
                led = led.Scale(_brightness);
            }
        }

        if (_flash is { } flash)
        {
            if (flash.IsOver(time))
            {
                _flash = null;
            }
            else
            {
                leds.Slice(flash.FirstLed, flash.LedCount).Fill(flash.ColorAt(time));
            }
        }
    }

    // Below 1, scale dims what goes to the device only (0 sends black): the buffer keeps the colors
    // of the effect, for the preview and for the send that gives them back.
    public void Send(double scale = 1.0)
    {
        if (scale >= 1.0)
        {
            Provider.SetLeds(Device.Id, Buffer);
            Buffer.AsSpan().CopyTo(_sent);
            return;
        }

        for (var i = 0; i < Buffer.Length; i++)
        {
            _sent[i] = Buffer[i].Scale(scale);
        }

        Provider.SetLeds(Device.Id, _sent);
    }

    public bool SendIfChanged()
    {
        if (Buffer.AsSpan().SequenceEqual(_sent))
        {
            return false;
        }

        Send();
        return true;
    }

    // Each LED of target gets the LED of image at the same place along it, measured at the middle
    // of the LED: a target as long as image gets a copy, a target of one LED the middle of image.
    private static void Stretch(ReadOnlySpan<RgbColor> image, Span<RgbColor> target)
    {
        for (var i = 0; i < target.Length; i++)
        {
            target[i] = image[(int)((2L * i + 1) * image.Length / (2L * target.Length))];
        }
    }

    // The zones as RgbZone.RangesOf counts them, without the ones without LEDs, and then the LEDs
    // after the last zone; the longest goes first, the first of them when more are as long.
    private static (int FirstLed, int LedCount)[] PartsOf(RgbDevice device)
    {
        var zones = RgbZone.RangesOf(device.Zones, device.LedCount);
        var end = zones.Length == 0 ? 0 : zones[^1].FirstLed + zones[^1].LedCount;
        List<(int FirstLed, int LedCount)> parts = [.. zones.Where(zone => zone.LedCount > 0)];
        if (end < device.LedCount)
        {
            parts.Add((end, device.LedCount - end));
        }

        var longest = 0;
        for (var part = 1; part < parts.Count; part++)
        {
            if (parts[part].LedCount > parts[longest].LedCount)
            {
                longest = part;
            }
        }

        if (longest > 0)
        {
            (parts[0], parts[longest]) = (parts[longest], parts[0]);
        }

        return [.. parts];
    }
}
