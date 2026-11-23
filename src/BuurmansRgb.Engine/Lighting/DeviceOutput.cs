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

    // A crossfade (BeginFade): the colors the device showed when it began, made once per output
    // and kept for the next fade, so a frame during a fade allocates nothing.
    private RgbColor[]? _fadeFrom;
    private bool _fading;
    private TimeSpan _fadeStart;
    private TimeSpan _fadeDuration;

    // The layer over the effect (ShowOverlay): the LEDs that have a color in it, within the LEDs of
    // the device, with those colors. Made when the layer changes, so a frame only copies them.
    private IReadOnlyList<RgbColor?>? _overlay;
    private int[] _overlayLeds = [];
    private RgbColor[] _overlayColors = [];

    public IDeviceProvider Provider { get; } = provider;

    public RgbDevice Device { get; } = device;

    public RgbColor[] Buffer { get; } = new RgbColor[device.LedCount];

    public EffectClock Clock { get; } = clock ?? new();

    // A device that flashes needs frames until the flash is over, whatever its effect, one with a
    // count that runs until the count stops, and one that fades until the fade is over.
    public bool IsAnimated => _effect.IsAnimated || _flash is not null || _count is { Running: true } || _fading;

    public bool IsFlashing => _flash is not null;

    public bool IsFading => _fading;

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

    // From the next render on, every LED with a color in overlay shows that color over the effect,
    // before the brightness, so a dimmed device dims its layer too; null shows the effect. An LED
    // past the end of the device, which may have fewer LEDs now, is skipped. Like Show, call it
    // after BeginFade, so a fade goes over from the layer before. The same list again does nothing.
    public void ShowOverlay(IReadOnlyList<RgbColor?> overlay)
    {
        if (ReferenceEquals(overlay, _overlay))
        {
            return;
        }

        _overlay = overlay;
        var count = 0;
        for (var i = 0; i < Math.Min(overlay.Count, Buffer.Length); i++)
        {
            count += overlay[i] is null ? 0 : 1;
        }

        if (count != _overlayLeds.Length)
        {
            _overlayLeds = new int[count];
            _overlayColors = new RgbColor[count];
        }

        var next = 0;
        for (var i = 0; i < Math.Min(overlay.Count, Buffer.Length); i++)
        {
            if (overlay[i] is { } color)
            {
                _overlayLeds[next] = i;
                _overlayColors[next] = color;
                next++;
            }
        }
    }

    // From time on, the renders show the flash over the effect on the LEDs firstLed to
    // firstLed + ledCount, such as one zone; the other LEDs keep showing the effect, and the
    // lighting itself stays.
    public void StartFlash(TimeSpan time, int firstLed, int ledCount) => _flash = new DeviceFlash(time, firstLed, ledCount);

    public void StopFlash() => _flash = null;

    // From time on, for duration, the renders go over from the colors the device shows at time,
    // in the middle of a fade too, to what its lighting shows then: call it before Show gives the
    // new effect. Without a flash or a count, which go over the fade as before. The fade runs on
    // the engine time, so a lower frame rate or a frame that comes late does not make it longer.
    public void BeginFade(TimeSpan time, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        var from = _fadeFrom ??= new RgbColor[Buffer.Length];
        RenderLighting(Buffer, time);
        Buffer.AsSpan().CopyTo(from);
        _fadeStart = time;
        _fadeDuration = duration;
        _fading = true;
    }

    // The next render shows the lighting itself, as at the end of the fade.
    public void EndFade() => _fading = false;

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
        RenderLighting(leds, time);

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

    // The effect with the layer over it at the brightness, over the colors a fade comes from as far
    // as it got.
    private void RenderLighting(Span<RgbColor> leds, TimeSpan time)
    {
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

        for (var i = 0; i < _overlayLeds.Length; i++)
        {
            leds[_overlayLeds[i]] = _overlayColors[i];
        }

        if (_brightness < 1.0)
        {
            foreach (ref var led in leds)
            {
                led = led.Scale(_brightness);
            }
        }

        if (_fading)
        {
            Fade(leds, time);
        }
    }

    // The last render of a fade shows the lighting itself, and ends it.
    private void Fade(Span<RgbColor> leds, TimeSpan time)
    {
        var amount = (time - _fadeStart) / _fadeDuration;
        if (amount >= 1.0)
        {
            _fading = false;
            return;
        }

        var from = _fadeFrom!;
        for (var i = 0; i < leds.Length; i++)
        {
            leds[i] = RgbColor.Lerp(from[i], leds[i], amount);
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
