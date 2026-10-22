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
    // Until the first Show; LightingController shows the lighting before it renders.
    private IEffect _effect = new StaticEffect(DeviceLighting.Default.Color);
    private double _brightness = DeviceLighting.Default.Brightness;
    private DeviceFlash? _flash;

    public IDeviceProvider Provider { get; } = provider;

    public RgbDevice Device { get; } = device;

    public RgbColor[] Buffer { get; } = new RgbColor[device.LedCount];

    public EffectClock Clock { get; } = clock ?? new();

    // A device that flashes needs frames until the flash is over, whatever its effect.
    public bool IsAnimated => _effect.IsAnimated || _flash is not null;

    public bool IsFlashing => _flash is not null;

    // Brightness runs from 0 to 1 and applies to whatever the effect renders; speed counts from
    // the next Render on.
    public void Show(IEffect effect, double brightness, double speed)
    {
        _effect = effect;
        _brightness = brightness;
        Clock.Speed = speed;
    }

    // From time on, the renders show the flash over the effect; the lighting itself stays.
    public void StartFlash(TimeSpan time) => _flash = new DeviceFlash(time);

    public void StopFlash() => _flash = null;

    // time is the engine time; the effect gets it at the device's speed. A flash goes over the
    // brightness, so a dimmed device still blinks bright; the first render after it shows the
    // effect again.
    public void Render(TimeSpan time)
    {
        var leds = Buffer.AsSpan();
        _effect.Render(leds, Clock.Tick(time));
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
                leds.Fill(flash.ColorAt(time));
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
}
