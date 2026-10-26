using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The LEDs of one zone counted one at a time, so the user can see how many a strip or fan on an
// addressable header has: LEDs 1 to Led burn, Led green and the ones before it white, the rest of
// the zone off; the other LEDs of the device keep showing the effect. Like DeviceFlash it runs on
// the engine time: while Running, Led goes up by LedsPerSecond each second from where it was set,
// whatever the frame rate, and it stops at the last LED. The zone is known by its index and name,
// so a device list that has another zone there does not get the count. Only used under the lock
// of LightingController.
internal sealed class ZoneCount
{
    public static RgbColor NewestColor { get; } = new(0, 255, 0);

    public static RgbColor CountedColor { get; } = RgbColor.White;

    private TimeSpan _since;
    private int _ledSince;

    // ledCount is how many LEDs the count goes up to, the longest the zone can be.
    public ZoneCount(string deviceId, int zoneIndex, string zoneName, int ledCount, int ledsPerSecond, TimeSpan time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ledCount, 1);
        ThrowIfInvalidPace(ledsPerSecond);
        DeviceId = deviceId;
        ZoneIndex = zoneIndex;
        ZoneName = zoneName;
        LedCount = ledCount;
        LedsPerSecond = ledsPerSecond;
        Running = ledCount > 1;
        _since = time;
        _ledSince = Led;
    }

    public string DeviceId { get; }

    public int ZoneIndex { get; }

    public string ZoneName { get; }

    public int LedCount { get; }

    // Counts from 1.
    public int Led { get; private set; } = 1;

    public int LedsPerSecond { get; private set; }

    public bool Running { get; private set; }

    public ZoneCountInfo Info => new(DeviceId, ZoneIndex, Led, LedCount, LedsPerSecond, Running);

    public static void ThrowIfInvalidPace(int ledsPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ledsPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ledsPerSecond, ZoneCountInfo.MaxLedsPerSecond);
    }

    // Moves Led to where the time says. Returns true when Led changed, or when the count stopped
    // at the last LED. Allocates nothing.
    public bool Advance(TimeSpan time)
    {
        if (!Running)
        {
            return false;
        }

        var steps = Math.Max(0L, (time - _since).Ticks * LedsPerSecond / TimeSpan.TicksPerSecond);
        var led = (int)Math.Min(LedCount, _ledSince + steps);
        var changed = led != Led;
        Led = led;
        if (Led >= LedCount)
        {
            Running = false;
            changed = true;
        }

        return changed;
    }

    // A null value stays as it is; Led is kept within 1 and LedCount. The pace counts from now, so
    // the next LED follows a whole step later. At the last LED the count does not run.
    public void Change(TimeSpan time, ZoneCountChange change)
    {
        if (change.LedsPerSecond is { } pace)
        {
            ThrowIfInvalidPace(pace);
        }

        Advance(time);
        Led = Math.Clamp(change.Led ?? Led, 1, LedCount);
        LedsPerSecond = change.LedsPerSecond ?? LedsPerSecond;
        Running = (change.Running ?? Running) && Led < LedCount;
        _since = time;
        _ledSince = Led;
    }

    // Draws the count over the LEDs of the zone, which the zone has now: a zone that is not at its
    // longest yet shows the LEDs it has.
    public void Render(Span<RgbColor> zone)
    {
        var counted = Math.Min(Led - 1, zone.Length);
        zone[..counted].Fill(CountedColor);
        if (counted < zone.Length)
        {
            zone[counted] = NewestColor;
            zone[(counted + 1)..].Fill(RgbColor.Black);
        }
    }
}
