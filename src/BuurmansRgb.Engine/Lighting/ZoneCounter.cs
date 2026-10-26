using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Counts the LEDs of one resizable zone, so the user can see how many a strip or fan on an
// addressable header has: the zone gets its longest length for a while (ZoneLengthKeeper
// .ResizeForAWhile) and LightingController draws one LED more at a time (StartCount). One zone at
// a time, for the settings app that started it (the owner).
//
// The zone gets the length it had back when the count stops: with Stop, when its owner goes (the
// app closes or its connection drops), when another zone starts counting, when the effects pause
// (for any reason: the tray menu, Windows locked or asleep, the screen off or the screen saver),
// before the devices are searched again (StopAll) and when the engine stops. ResizeZone of the
// counted zone ends it with the length the user chose instead ("Dit is de laatste"). A device list
// that does not have the zone as the count left it (the device went, another zone is at its index,
// or the zone got another length from elsewhere) ends the count without a length: the device is
// not there to get one, or got one from elsewhere. A device list that only brings the longest
// length, or that is made again for another reason, leaves the count running.
//
// Thread-safe. It calls the keeper and the lighting under its own lock, never the other way round:
// the lighting raises its events outside its lock and fetches device lists on another thread.
internal sealed class ZoneCounter : IDisposable
{
    private readonly LightingController _lighting;
    private readonly ZoneLengthKeeper _lengths;
    private readonly Lock _gate = new();
    private Counting? _counting;

    public ZoneCounter(LightingController lighting, ZoneLengthKeeper lengths)
    {
        _lighting = lighting;
        _lengths = lengths;
        _lighting.DevicesChanged += OnDevicesChanged;
        _lighting.PauseChanged += OnPauseChanged;
    }

    // Starts counting the zone (an index into the device's zones) from LED 1 at ledsPerSecond, up
    // to its longest length; a count of another zone stops first and gets its length back. The
    // same zone once more starts again at LED 1, for the new owner. While the effects pause
    // nothing starts. Throws ArgumentException for a device the engine does not know, a zone it
    // does not have or whose length cannot be set, and a pace outside 1 to
    // ZoneCountInfo.MaxLedsPerSecond.
    public void Start(string deviceId, int zoneIndex, int ledsPerSecond, object? owner = null)
    {
        ZoneCount.ThrowIfInvalidPace(ledsPerSecond);
        lock (_gate)
        {
            var device = _lighting.Devices.FirstOrDefault(candidate => candidate.Id == deviceId) ?? throw DeviceErrors.Unknown(deviceId);
            var zone = device.GetZone(zoneIndex);
            if (!zone.Resizable)
            {
                throw new ArgumentException($"Zone '{zone.Name}' of device '{deviceId}' cannot be resized.", nameof(zoneIndex));
            }

            if (_counting is { } counting && counting.Counts(deviceId, zoneIndex))
            {
                if (_lighting.StartCount(deviceId, zoneIndex, counting.LedCount, ledsPerSecond))
                {
                    _counting = counting with { Owner = owner };
                }
                else
                {
                    EndLocked(restore: true);
                }

                return;
            }

            EndLocked(restore: true);
            if (!_lighting.StartCount(deviceId, zoneIndex, zone.MaxLedCount, ledsPerSecond))
            {
                return;
            }

            _counting = new Counting(owner, deviceId, zoneIndex, zone.Name, zone.LedCount, zone.MaxLedCount);
            try
            {
                _lengths.ResizeForAWhile(deviceId, zoneIndex, zone.MaxLedCount);
            }
            catch
            {
                _counting = null;
                _lighting.StopCount();
                throw;
            }
        }
    }

    // Pauses, goes on, goes to another LED or takes another pace; a request for a zone that is not
    // counted (any more) changes nothing, as the app may be behind.
    public void Change(string deviceId, int zoneIndex, ZoneCountChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.LedsPerSecond is { } pace)
        {
            ZoneCount.ThrowIfInvalidPace(pace);
        }

        lock (_gate)
        {
            if (_counting?.Counts(deviceId, zoneIndex) == true)
            {
                _lighting.ChangeCount(change);
            }
        }
    }

    // Stops the count of the zone and gives it its length back; nothing for another zone.
    public void Stop(string deviceId, int zoneIndex)
    {
        lock (_gate)
        {
            if (_counting?.Counts(deviceId, zoneIndex) == true)
            {
                EndLocked(restore: true);
            }
        }
    }

    // Stops a count that owner started, such as when its settings app goes.
    public void StopFor(object owner)
    {
        lock (_gate)
        {
            if (_counting is { } counting && ReferenceEquals(counting.Owner, owner))
            {
                EndLocked(restore: true);
            }
        }
    }

    // Stops any count, such as before the devices are searched again.
    public void StopAll()
    {
        lock (_gate)
        {
            EndLocked(restore: true);
        }
    }

    // The length the user chose (ZoneLengthKeeper.ResizeZone), which ends a count of that zone
    // without giving it its old length first. When the length is refused, the count still ends,
    // and the zone gets its old length back.
    public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
    {
        lock (_gate)
        {
            if (_counting is not { } counting || !counting.Counts(deviceId, zoneIndex))
            {
                _lengths.ResizeZone(deviceId, zoneIndex, ledCount);
                return;
            }

            _counting = null;
            _lighting.StopCount();
            try
            {
                _lengths.ResizeZone(deviceId, zoneIndex, ledCount);
            }
            catch
            {
                Restore(counting);
                throw;
            }
        }
    }

    // The engine stops: the zone gets its length back.
    public void Dispose()
    {
        _lighting.DevicesChanged -= OnDevicesChanged;
        _lighting.PauseChanged -= OnPauseChanged;
        StopAll();
    }

    private void EndLocked(bool restore)
    {
        if (_counting is not { } counting)
        {
            return;
        }

        _counting = null;
        _lighting.StopCount();
        if (restore)
        {
            Restore(counting);
        }
    }

    // Best effort: a zone that went or changed meanwhile keeps what it has.
    private void Restore(Counting counting)
    {
        var device = _lighting.Devices.FirstOrDefault(candidate => candidate.Id == counting.DeviceId);
        if (device is null || counting.ZoneIndex >= device.Zones.Count || device.Zones[counting.ZoneIndex].Name != counting.ZoneName)
        {
            return;
        }

        try
        {
            _lengths.ResizeForAWhile(counting.DeviceId, counting.ZoneIndex, counting.OldLength);
        }
        catch (ArgumentException exception)
        {
            Trace.TraceWarning($"Could not give zone '{counting.ZoneName}' of '{counting.DeviceId}' its length back after counting: {exception.Message}");
        }
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (_counting is not { } counting)
            {
                return;
            }

            var device = _lighting.Devices.FirstOrDefault(candidate => candidate.Id == counting.DeviceId);
            var zone = device is not null && counting.ZoneIndex < device.Zones.Count ? device.Zones[counting.ZoneIndex] : null;
            if (zone is not null && zone.Name == counting.ZoneName && zone.MaxLedCount == counting.LedCount)
            {
                // At its longest, or still at its old length while the longest is on its way.
                if (zone.LedCount == counting.LedCount)
                {
                    counting.Reached = true;
                    return;
                }

                if (!counting.Reached && zone.LedCount == counting.OldLength)
                {
                    return;
                }
            }

            EndLocked(restore: false);
        }
    }

    private void OnPauseChanged(object? sender, PauseReasons pause)
    {
        if (pause != PauseReasons.None)
        {
            StopAll();
        }
    }

    // The zone that is counted, with the length it had before and the longest one.
    private sealed record Counting(object? Owner, string DeviceId, int ZoneIndex, string ZoneName, int OldLength, int LedCount)
    {
        // Whether a device list brought the longest length; an old length after that came from elsewhere.
        public bool Reached { get; set; }

        public bool Counts(string deviceId, int zoneIndex) => DeviceId == deviceId && ZoneIndex == zoneIndex;
    }
}
