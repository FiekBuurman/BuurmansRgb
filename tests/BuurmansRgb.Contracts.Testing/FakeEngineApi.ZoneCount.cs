namespace BuurmansRgb.Contracts.Testing;

// Counting the LEDs of a zone, and the length of a zone that ends a count.
public sealed partial class FakeEngineApi
{
    public ZoneCountInfo ZoneCount { get; set; } = ZoneCountInfo.None;

    public event EventHandler<ZoneCountInfo>? ZoneCountChanged;

    public bool HasZoneCountSubscribers => ZoneCountChanged is not null;

    public void RaiseZoneCount(ZoneCountInfo count)
    {
        ZoneCount = count;
        ZoneCountChanged?.Invoke(this, count);
    }

    public Task<ZoneCountInfo> GetZoneCountAsync(CancellationToken ct = default) => Task.FromResult(ZoneCount);

    // Like the engine: a count of the zone ends with this length.
    public async Task ResizeZoneAsync(string deviceId, int zoneIndex, int ledCount, CancellationToken ct = default)
    {
        await Record($"resize {deviceId} {zoneIndex} {ledCount}");
        if (ZoneCount.Counts(deviceId, zoneIndex))
        {
            RaiseZoneCount(ZoneCountInfo.None);
        }
    }

    // Like the engine: the count starts at LED 1 and goes up to the longest length of the zone.
    public async Task StartZoneCountAsync(string deviceId, int zoneIndex, int ledsPerSecond, CancellationToken ct = default)
    {
        await Record($"count {deviceId} {zoneIndex} {ledsPerSecond}");
        var zone = Devices.Single(device => device.Id == deviceId).Zones[zoneIndex];
        RaiseZoneCount(new ZoneCountInfo(deviceId, zoneIndex, 1, zone.MaxLedCount, ledsPerSecond, true));
    }

    public async Task SetZoneCountAsync(string deviceId, int zoneIndex, ZoneCountChange change, CancellationToken ct = default)
    {
        var values = new[]
        {
            change.Led is { } led ? $"led {led}" : null,
            change.Running is { } running ? (running ? "run" : "pause") : null,
            change.LedsPerSecond is { } pace ? $"pace {pace}" : null,
        };
        await Record($"count {deviceId} {zoneIndex} {string.Join(' ', values.OfType<string>())}");
        if (ZoneCount.Counts(deviceId, zoneIndex))
        {
            var count = ZoneCount with
            {
                Led = Math.Clamp(change.Led ?? ZoneCount.Led, 1, ZoneCount.LedCount),
                Running = change.Running ?? ZoneCount.Running,
                LedsPerSecond = change.LedsPerSecond ?? ZoneCount.LedsPerSecond,
            };
            RaiseZoneCount(count);
        }
    }

    public async Task StopZoneCountAsync(string deviceId, int zoneIndex, CancellationToken ct = default)
    {
        await Record($"count {deviceId} {zoneIndex} stop");
        if (ZoneCount.Counts(deviceId, zoneIndex))
        {
            RaiseZoneCount(ZoneCountInfo.None);
        }
    }
}
