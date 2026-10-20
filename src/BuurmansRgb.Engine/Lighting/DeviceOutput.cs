using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// One device with the effect it shows and its LED buffers. The buffers are made once per device
// list, so rendering a frame allocates nothing. Only used under the lock of LightingController.
internal sealed class DeviceOutput(IDeviceProvider provider, RgbDevice device)
{
    // What the device got last, so a frame that changes nothing is not sent.
    private readonly RgbColor[] _sent = new RgbColor[device.LedCount];
    // Until the first Show; LightingController shows the lighting before it renders.
    private IEffect _effect = new StaticEffect(DeviceLighting.Default.Color);
    private double _brightness = DeviceLighting.Default.Brightness;

    public IDeviceProvider Provider { get; } = provider;

    public RgbDevice Device { get; } = device;

    public RgbColor[] Buffer { get; } = new RgbColor[device.LedCount];

    public bool IsAnimated => _effect.IsAnimated;

    // Brightness runs from 0 to 1 and applies to whatever the effect renders.
    public void Show(IEffect effect, double brightness)
    {
        _effect = effect;
        _brightness = brightness;
    }

    public void Render(TimeSpan time)
    {
        var leds = Buffer.AsSpan();
        _effect.Render(leds, time);
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
