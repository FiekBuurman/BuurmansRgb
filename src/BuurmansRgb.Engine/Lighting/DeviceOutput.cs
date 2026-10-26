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
    private readonly (int FirstLed, int LedCount, int Part)[] _parts = PartsOf(device);

    // Until the first Show; LightingController shows the lighting before it renders.
    private IEffect _effect = new StaticEffect(DeviceLighting.Default.Color);
    private double _brightness = DeviceLighting.Default.Brightness;
    private bool _zonesAlike;

    // With a direction, the place of every LED along it (LedPlaces.Along), and that of the LEDs
    // of the longest part on their own, for zones alike. Made again only when the direction
    // changes; a new device list makes a new output.
    private int? _direction;
    private double[]? _positions;
    private double[]? _imagePositions;
    private DeviceFlash? _flash;
    private ZoneCount? _count;
    private int _countFirstLed;
    private int _countLedCount;

    public IDeviceProvider Provider { get; } = provider;

    public RgbDevice Device { get; } = device;

    public RgbColor[] Buffer { get; } = new RgbColor[device.LedCount];

    public EffectClock Clock { get; } = clock ?? new();

    // A device that flashes needs frames until the flash is over, whatever its effect, and one
    // with a count that runs until the count stops.
    public bool IsAnimated => _effect.IsAnimated || _flash is not null || _count is { Running: true };

    public bool IsFlashing => _flash is not null;

    public bool IsCounting => _count is not null;

    // Brightness runs from 0 to 1 and applies to whatever the effect renders; speed counts from
    // the next Render on. With zonesAlike the effect renders once, over the longest zone, and
    // every other zone gets that image stretched to its own length, so a mouse with two zones of
    // one LED shows one color in a rainbow. A device with fewer than two zones with LEDs looks the
    // same either way. With a direction in degrees, an effect that can run in one
    // (IDirectedEffect) runs over where the LEDs sit; without one, or for another effect, over
    // the LEDs in their order, as before there was a direction.
    public void Show(IEffect effect, double brightness, double speed, bool zonesAlike = false, int? direction = null)
    {
        _effect = effect;
        _brightness = brightness;
        _zonesAlike = zonesAlike;
        Clock.Speed = speed;
        Aim(direction);
    }

    // From time on, the renders show the flash over the effect on the LEDs firstLed to
    // firstLed + ledCount, such as one zone; the other LEDs keep showing the effect, and the
    // lighting itself stays.
    public void StartFlash(TimeSpan time, int firstLed, int ledCount) => _flash = new DeviceFlash(time, firstLed, ledCount);

    public void StopFlash() => _flash = null;

    // From now on, the renders show the count over the effect on the LEDs firstLed to
    // firstLed + ledCount: the zone that is counted, as long as the device has it now. Null ends
    // it. The count itself moves on in LightingController, so it goes on when the device list is
    // made again.
    public void ShowCount(ZoneCount? count, int firstLed = 0, int ledCount = 0)
    {
        _count = count;
        _countFirstLed = firstLed;
        _countLedCount = ledCount;
    }

    // time is the engine time; the effect gets it at the device's speed. A count and a flash go
    // over the brightness, so a dimmed device still shows them bright, and a flash goes over a
    // count; the first render after a flash shows the effect again.
    public void Render(TimeSpan time)
    {
        var leds = Buffer.AsSpan();
        var effectTime = Clock.Tick(time);
        if (_zonesAlike && _parts.Length > 1)
        {
            var image = leds.Slice(_parts[0].FirstLed, _parts[0].LedCount);
            RenderEffect(image, _imagePositions, effectTime);
            for (var part = 1; part < _parts.Length; part++)
            {
                Stretch(image, leds.Slice(_parts[part].FirstLed, _parts[part].LedCount));
            }
        }
        else
        {
            RenderEffect(leds, _positions, effectTime);
        }

        if (_brightness < 1.0)
        {
            foreach (ref var led in leds)
            {
                led = led.Scale(_brightness);
            }
        }

        _count?.Render(leds.Slice(_countFirstLed, _countLedCount));

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

    private void RenderEffect(Span<RgbColor> leds, double[]? positions, TimeSpan time)
    {
        if (positions is not null && _effect is IDirectedEffect directed)
        {
            directed.Render(leds, positions, time);
        }
        else
        {
            _effect.Render(leds, time);
        }
    }

    private void Aim(int? direction)
    {
        if (direction == _direction)
        {
            return;
        }

        _direction = direction;
        if (direction is not { } degrees)
        {
            (_positions, _imagePositions) = (null, null);
            return;
        }

        _positions = LedPlaces.Along(Device, degrees);
        if (_parts.Length > 1)
        {
            var (_, count, part) = _parts[0];
            _imagePositions = LedPlaces.Along(Device.Type, LedPlaces.ZoneOfPart(Device.Zones, part, count), count, degrees);
        }
    }

    // The parts as RgbZone.PartsOf counts them, as the preview does, without the ones without
    // LEDs, with the index of each in PartsOf; the longest goes first, the first of them when more
    // are as long.
    private static (int FirstLed, int LedCount, int Part)[] PartsOf(RgbDevice device)
    {
        (int FirstLed, int LedCount, int Part)[] parts =
            [.. RgbZone.PartsOf(device.Zones, device.LedCount)
                .Select((part, index) => (part.FirstLed, part.LedCount, index))
                .Where(part => part.LedCount > 0)];
        var longest = 0;
        for (var part = 1; part < parts.Length; part++)
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

        return parts;
    }
}
