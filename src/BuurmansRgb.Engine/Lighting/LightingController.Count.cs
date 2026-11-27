using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// The LEDs of one zone that are counted one by one (ZoneCountRun).
internal sealed partial class LightingController
{
    // The LEDs of the zone that are counted (StartCount); one zone at a time.
    private ZoneCountRun? _count;

    // Grows with every change of the count, under the lock, so a listener can drop a state that
    // arrives late (ZoneCountInfo.Sequence): CountChanged goes out from the render thread and from
    // IPC threads.
    private long _countSequence;

    // Raised when a count of LEDs starts, goes to another LED, changes or ends; read CurrentZoneCount for
    // the newest state. Raised from the render loop too, without allocating.
    public event EventHandler? CountChanged;

    // The count of LEDs as it is now, with the sequence of its last change; ZoneCountInfo.None
    // (with that sequence) while no zone is counted.
    public ZoneCountInfo CurrentZoneCount
    {
        get
        {
            lock (_gate)
            {
                return (_count?.Info ?? ZoneCountInfo.None) with { Sequence = _countSequence };
            }
        }
    }

    // Counts the LEDs of one zone (ZoneCountRun) from LED 1 on, up to ledCount, at ledsPerSecond:
    // only the drawing; ZoneCounter gives the zone its longest length for it and its own length
    // back. It replaces the count of any zone and changes no lighting. While paused, or for a
    // device or zone the engine does not know (any more), nothing starts and it returns false. A
    // device list that is made again keeps the count on the zone with the same index and key.
    public bool StartCount(string deviceId, int zoneIndex, int ledCount, int ledsPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ledCount, 1);
        lock (_gate)
        {
            if (_outputs.Find(deviceId) is not { } output || _pause != PauseReasons.None || !_lighting.IsSet
                || !output.Device.TryGetZone(zoneIndex, out var zone))
            {
                return false;
            }

            // ZoneCountRun checks the pace, before the count that runs stops.
            var time = EffectTime;
            var count = new ZoneCountRun(deviceId, zoneIndex, zone.Key, zone.Name, ledCount, ledsPerSecond, time);
            StopCountLocked();
            _count = count;
            _countSequence++;
            ShowCountLocked(output, time);
            UpdateAnimationLocked();
        }

        Publish(true);
        CountChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // Changes the count that runs, such as a pause or another LED; false when none runs.
    public bool ChangeCount(ZoneCountChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            if (_count is not { } count)
            {
                return false;
            }

            var time = EffectTime;
            count.Change(time, change);
            _countSequence++;
            if (_outputs.Find(count.DeviceId) is { } output)
            {
                ShowCountLocked(output, time);
            }

            UpdateAnimationLocked();
        }

        Publish(true);
        CountChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // Ends the count, so the zone shows the effect again.
    public void StopCount()
    {
        bool stopped;
        lock (_gate)
        {
            stopped = StopCountLocked();
            UpdateAnimationLocked();
        }

        Publish(stopped);
        if (stopped)
        {
            CountChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Draws the count over the zone of output as the device has it now, and sends it.
    private void ShowCountLocked(DeviceOutput output, TimeSpan time)
    {
        AttachCountLocked(output);
        output.Render(time);
        output.Send(SendScaleLocked);
        _sequence++;
    }

    // Gives the output of the counted device the count, over the LEDs its zone has now; a device
    // list without the zone (with the same index and key) leaves it without, until ZoneCounter
    // ends the count.
    private void AttachCountLocked(DeviceOutput? output = null)
    {
        if (_count is not { } count || (output ?? _outputs.Find(count.DeviceId)) is not { } counted)
        {
            return;
        }

        if (counted.Device.TryGetZone(count.ZoneIndex, out var zone) && zone.Key == count.ZoneKey)
        {
            var (first, ledCount) = RgbZone.RangeOf(counted.Device.Zones, counted.Device.LedCount, count.ZoneIndex);
            counted.ShowCount(count, first, ledCount);
        }
        else
        {
            counted.ShowCount(null);
        }
    }

    // Gives the counted device its own colors again. Returns true when a zone was counted.
    private bool StopCountLocked()
    {
        if (_count is not { } count)
        {
            return false;
        }

        _count = null;
        _countSequence++;
        if (_outputs.Find(count.DeviceId) is { } output)
        {
            output.ShowCount(null);
            output.Render(EffectTime);
            output.Send(SendScaleLocked);
        }

        _sequence++;
        return true;
    }
}
