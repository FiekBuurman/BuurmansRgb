using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

internal sealed class LightingController(IReadOnlyList<IDeviceProvider> providers)
{
    private readonly List<DeviceOutput> _outputs = [];
    private RgbColor? _color;

    public bool IsPaused { get; private set; }

    public async Task DiscoverAsync(CancellationToken ct)
    {
        _outputs.Clear();
        foreach (var provider in providers)
        {
            foreach (var device in await provider.DiscoverAsync(ct))
            {
                _outputs.Add(new(provider, device, new RgbColor[device.LedCount]));
            }
        }
    }

    public void ApplyStaticColor(RgbColor color, double brightness)
    {
        _color = color.Scale(brightness);
        Render();
    }

    // Resuming sends the latest color again; pausing leaves the devices as they are.
    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        Render();
    }

    private void Render()
    {
        if (IsPaused || _color is not { } color)
        {
            return;
        }

        foreach (var (provider, device, buffer) in _outputs)
        {
            buffer.AsSpan().Fill(color);
            provider.SetLeds(device.Id, buffer);
        }
    }

    private sealed record DeviceOutput(IDeviceProvider Provider, RgbDevice Device, RgbColor[] Buffer);
}
