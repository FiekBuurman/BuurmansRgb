using System.Diagnostics;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Remembers the length the user gave each resizable zone and gives it back whenever a device
// reports another one: OpenRGB may forget a length after a restart, and the simulated devices start
// with their first length again. Zones are known by name, not by index: the name stays the same when
// a provider lists the zones in another order, and a zone whose name is gone (or that two zones of
// the device share) is left alone instead of getting the length of another zone. The engine asks a
// device for a length once each time the device shows up, and again only after the device reported
// that length: a provider that keeps refusing it cannot make a loop of resizes and new device lists.
// A length the zone cannot hold (any more) is kept, but not asked for. LedColors and the lighting
// are not involved. Thread-safe; the lighting is called outside the lock.
internal sealed class ZoneLengthKeeper : IDisposable
{
    private readonly LightingController _lighting;
    private readonly Lock _gate = new();

    // By device id, the length of each zone by its name.
    private readonly Dictionary<string, Dictionary<string, int>> _lengths = [];

    // The lengths asked for and not reported yet, or that the zone could not hold, by device and
    // zone name. A device that goes loses its entries, so it is asked again when it comes back.
    private readonly Dictionary<(string DeviceId, string Zone), int> _asked = [];

    public ZoneLengthKeeper(LightingController lighting)
    {
        _lighting = lighting;
        _lighting.DevicesChanged += OnDevicesChanged;
    }

    // Raised when the user set a length, so it can be saved; read Lengths for all of them.
    public event EventHandler? LengthsChanged;

    // A copy, by device id and zone name.
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Lengths
    {
        get
        {
            lock (_gate)
            {
                return _lengths.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, int>)new Dictionary<string, int>(pair.Value));
            }
        }
    }

    // The lengths from the settings, before the devices are fetched: the devices get them as they
    // show up. Replaces what was known.
    public void Load(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> lengths)
    {
        lock (_gate)
        {
            _lengths.Clear();
            _asked.Clear();
            foreach (var (deviceId, zones) in lengths)
            {
                _lengths[deviceId] = new Dictionary<string, int>(zones);
            }
        }
    }

    // The user's choice: the zone gets the length, and keeps it from now on. Throws like
    // LightingController.ResizeZone, and then remembers nothing.
    // It is remembered before the provider gets it: a provider may report the new length before
    // this returns, and the old length must not come back then. A zone name the device has twice
    // gives nothing to remember it by, so that zone only gets the length.
    public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
    {
        var device = _lighting.Devices.FirstOrDefault(candidate => candidate.Id == deviceId);
        var zone = device is not null && zoneIndex >= 0 && zoneIndex < device.Zones.Count && IndexOf(device, device.Zones[zoneIndex].Name) == zoneIndex
            ? device.Zones[zoneIndex].Name
            : null;
        if (zone is null)
        {
            _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
            return;
        }

        var key = (deviceId, zone);
        Dictionary<string, int>? before;
        bool wasAsked;
        int asked;
        lock (_gate)
        {
            before = _lengths.TryGetValue(deviceId, out var zones) ? new Dictionary<string, int>(zones) : null;
            wasAsked = _asked.TryGetValue(key, out asked);
            if (zones is null)
            {
                zones = [];
                _lengths[deviceId] = zones;
            }

            zones[zone] = ledCount;
            _asked[key] = ledCount;
        }

        try
        {
            _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
        }
        catch
        {
            lock (_gate)
            {
                RestoreLocked(deviceId, before);
                if (wasAsked)
                {
                    _asked[key] = asked;
                }
                else
                {
                    _asked.Remove(key);
                }
            }

            throw;
        }

        LengthsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _lighting.DevicesChanged -= OnDevicesChanged;

    private void RestoreLocked(string deviceId, Dictionary<string, int>? zones)
    {
        if (zones is null)
        {
            _lengths.Remove(deviceId);
        }
        else
        {
            _lengths[deviceId] = zones;
        }
    }

    private static int IndexOf(RgbDevice device, string zone)
    {
        var index = -1;
        for (var candidate = 0; candidate < device.Zones.Count; candidate++)
        {
            if (device.Zones[candidate].Name == zone)
            {
                if (index >= 0)
                {
                    return -1;
                }

                index = candidate;
            }
        }

        return index;
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        List<(string DeviceId, int ZoneIndex, int LedCount)> resizes = [];
        var devices = _lighting.Devices;
        lock (_gate)
        {
            foreach (var key in _asked.Keys.Where(key => !devices.Any(device => device.Id == key.DeviceId)).ToList())
            {
                _asked.Remove(key);
            }

            foreach (var device in devices)
            {
                if (_lengths.TryGetValue(device.Id, out var zones))
                {
                    AskLocked(device, zones, resizes);
                }
            }
        }

        foreach (var (deviceId, zoneIndex, ledCount) in resizes)
        {
            try
            {
                _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
            }
            catch (ArgumentException exception)
            {
                // The device went or changed after this list; the next list decides again.
                Trace.TraceWarning($"Could not give a zone of '{deviceId}' its length again: {exception.Message}");
            }
        }
    }

    private void AskLocked(RgbDevice device, Dictionary<string, int> zones, List<(string, int, int)> resizes)
    {
        foreach (var (name, ledCount) in zones)
        {
            var index = IndexOf(device, name);
            if (index < 0)
            {
                continue;
            }

            var key = (device.Id, name);
            var zone = device.Zones[index];
            if (zone.LedCount == ledCount)
            {
                _asked.Remove(key);
                continue;
            }

            if (_asked.ContainsKey(key))
            {
                continue;
            }

            _asked[key] = ledCount;
            if (zone.Resizable && zone.CanHold(ledCount))
            {
                resizes.Add((device.Id, index, ledCount));
            }
            else
            {
                Trace.TraceWarning($"Zone '{name}' of '{device.Id}' holds {zone.MinLedCount} to {zone.MaxLedCount} LEDs now, not the {ledCount} it had; it keeps {zone.LedCount}.");
            }
        }
    }
}
