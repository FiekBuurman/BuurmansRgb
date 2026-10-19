using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// One device with the effect it shows and its LED buffer. The buffer is made once per device
// list, so rendering a frame allocates nothing. Only used under the lock of LightingController.
internal sealed class DeviceOutput(IDeviceProvider provider, RgbDevice device)
{
    private IEffect _effect = EffectCatalog.Create(DeviceLighting.Default.EffectId, DeviceLighting.Default.Color, []);
    private double _brightness = DeviceLighting.Default.Brightness;

    public IDeviceProvider Provider { get; } = provider;

    public RgbDevice Device { get; } = device;

    public RgbColor[] Buffer { get; } = new RgbColor[device.LedCount];

    public bool IsAnimated => _effect.IsAnimated;

    public void Show(DeviceLighting lighting)
    {
        _effect = EffectCatalog.Create(lighting.EffectId, lighting.Color, []);
        _brightness = lighting.Brightness;
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

    public void Send() => Provider.SetLeds(Device.Id, Buffer);
}
