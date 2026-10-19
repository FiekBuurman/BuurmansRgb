namespace BuurmansRgb.Core;

public interface IDeviceProvider
{
    string Name { get; }

    Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct);

    void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors);
}
