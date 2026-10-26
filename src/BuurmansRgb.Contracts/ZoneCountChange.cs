namespace BuurmansRgb.Contracts;

// What changes in a count of LEDs (IEngineApi.SetZoneCountAsync); null leaves it as the engine has
// it. Led goes to that LED (within 1 and the zone's LedCount), Running pauses or goes on, and
// LedsPerSecond sets the pace, from 1 to ZoneCountInfo.MaxLedsPerSecond.
public sealed record ZoneCountChange(int? Led = null, bool? Running = null, int? LedsPerSecond = null);
