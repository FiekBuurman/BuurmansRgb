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

    public IDeviceProvider Provider { get; } = provider;

    public RgbDevice Device { get; } = device;

    public RgbColor[] Buffer { get; } = new RgbColor[device.LedCount];

    public EffectClock Clock { get; } = clock ?? new();

    public bool IsAnimated => _effect.IsAnimated;

    // Brightness runs from 0 to 1 and applies to whatever the effect renders; speed counts from
    // the next Render on.
    public void Show(IEffect effect, double brightness, double speed)
    {
        _effect = effect;
        _brightness = brightness;
        Clock.Speed = speed;
    }

    // time is the engine time; the effect gets it at the device's speed.
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
    }

    public void Send()
    {
        Provider.SetLeds(Device.Id, Buffer);
        Buffer.AsSpan().CopyTo(_sent);
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
