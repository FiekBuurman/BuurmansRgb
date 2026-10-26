using System.Text.Json.Serialization;

namespace BuurmansRgb.Contracts;

// How the LEDs of a resizable zone are counted (IEngineApi.StartZoneCountAsync), so the user can
// see how many a strip or fan on an addressable header has: the zone is at its longest, LedCount
// LEDs, and its LEDs 1 to Led burn, Led itself green and the ones before it white. While Running,
// Led goes up by LedsPerSecond each second until the last LED; then it stops. None while no zone
// is counted.
public sealed record ZoneCountInfo(string DeviceId, int ZoneIndex, int Led, int LedCount, int LedsPerSecond, bool Running)
{
    public const int DefaultLedsPerSecond = 1;
    public const int MaxLedsPerSecond = 10;

    public static ZoneCountInfo None { get; } = new(string.Empty, -1, 0, 0, DefaultLedsPerSecond, false);

    // Grows with every change of the count in the engine, also when it ends, so a client can drop
    // a state that arrives after a newer one: ZoneCountChanged goes out from more than one thread.
    // Starts again when the engine restarts.
    public long Sequence { get; init; }

    [JsonIgnore]
    public bool IsCounting => DeviceId.Length > 0;

    public bool Counts(string deviceId, int zoneIndex) => IsCounting && DeviceId == deviceId && ZoneIndex == zoneIndex;
}
