using System.Diagnostics;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Lighting;

// Remembers the length the user gave each resizable zone and gives it back when a device shows up
// (again) with another one: OpenRGB may forget a length after a restart, and the simulated devices
// start with their first length again. A device that was there already and reports another length
// that the engine did not ask for got it from elsewhere, such as the app of OpenRGB itself: that
// length is remembered from then on, except 0, which looks more like a length that was forgotten.
// Zones are known by name, not by index: the name stays the same when a provider lists the zones in
// another order, and a zone whose name is gone (or that two zones of the device share) is left
// alone instead of getting the length of another zone. The engine asks a device for a length only
// when the device shows up (or settings.json changed by hand), and waits until the device reported
// it: a provider that refuses it cannot make a loop of resizes and new device lists, nor is its
// refusal taken for a length from elsewhere. A length the zone cannot hold (any more) is kept, but
// not asked for. LedColors and the lighting are not involved. Thread-safe. The provider gets a
// length under the lock, so a length the user sets and one given back from a device list cannot
// overtake each other; the lighting never calls into this while it holds its own lock, and fetches
// a new device list on another thread.
internal sealed class ZoneLengthKeeper : IDisposable
{
    private readonly LightingController _lighting;
    private readonly Lock _gate = new();

    // By device id, the length of each zone by its name.
    private readonly Dictionary<string, Dictionary<string, int>> _lengths = [];

    // The lengths asked for and not reported yet, by device and zone name. A device that goes loses
    // its entries.
    private readonly Dictionary<(string DeviceId, string Zone), int> _asked = [];

    // The zone lengths each device that is there reported last, by zone name, to tell a device that
    // shows up from one that was there already.
    private readonly Dictionary<string, Dictionary<string, int>> _reported = [];

    public ZoneLengthKeeper(LightingController lighting)
    {
        _lighting = lighting;
        _lighting.DevicesChanged += OnDevicesChanged;
    }

    // Raised when the user set a length, here or elsewhere, so it can be saved; read Lengths for all
    // of them.
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

    // The length remembered for the zone with that name; null when there is none.
    public int? LengthOf(string deviceId, string zone)
    {
        lock (_gate)
        {
            return _lengths.TryGetValue(deviceId, out var zones) && zones.TryGetValue(zone, out var ledCount) ? ledCount : null;
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
            _reported.Clear();
            foreach (var (deviceId, zones) in lengths)
            {
                _lengths[deviceId] = new Dictionary<string, int>(zones);
            }
        }
    }

    // The lengths from a settings file that changed by hand, while the devices are there: replaces
    // what was known, and a device gets a length it does not have, as if it showed up again.
    // Raises LengthsChanged when the lengths are others than the ones known.
    public void Change(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> lengths)
    {
        List<(string DeviceId, int ZoneIndex, int LedCount)> resizes = [];
        lock (_gate)
        {
            if (SameLengthsLocked(lengths))
            {
                return;
            }

            _lengths.Clear();
            foreach (var (deviceId, zones) in lengths)
            {
                _lengths[deviceId] = new Dictionary<string, int>(zones);
            }

            foreach (var device in _lighting.Devices)
            {
                AskLocked(device, resizes);
            }

            ResizeLocked(resizes);
        }

        LengthsChanged?.Invoke(this, EventArgs.Empty);
    }

    // The user's choice: the zone gets the length, and keeps it from now on. Throws like
    // LightingController.ResizeZone, and then remembers nothing. It is remembered before the
    // provider gets it, as a provider may report the new length before this returns. A zone name
    // the device has twice gives nothing to remember it by, so that zone only gets the length.
    public void ResizeZone(string deviceId, int zoneIndex, int ledCount)
    {
        lock (_gate)
        {
            var device = _lighting.Devices.FirstOrDefault(candidate => candidate.Id == deviceId);
            var zone = device is not null && zoneIndex >= 0 && zoneIndex < device.Zones.Count && IndexOf(device, device.Zones[zoneIndex].Name) == zoneIndex
                ? device.Zones[zoneIndex]
                : null;
            if (zone is null)
            {
                _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
                return;
            }

            var key = (deviceId, zone.Name);
            var before = _lengths.TryGetValue(deviceId, out var zones) ? new Dictionary<string, int>(zones) : null;
            var wasAsked = _asked.TryGetValue(key, out var asked);
            RememberLocked(deviceId, zone.Name, ledCount);

            // A length the zone has already brings no new device list to wait for.
            if (zone.LedCount != ledCount)
            {
                _asked[key] = ledCount;
            }

            try
            {
                _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
            }
            catch
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

                throw;
            }
        }

        LengthsChanged?.Invoke(this, EventArgs.Empty);
    }

    // A length for a while, such as the longest while the user counts the LEDs of the zone
    // (ZoneCounter), and the length it had before afterwards: the zone gets it, but it is not
    // remembered, and when the device reports it, it is not taken for a length from elsewhere. A
    // zone without a remembered length gets the one it has now remembered first, so a device that
    // shows up again before the zone has its own length back, for example after the engine stopped
    // halfway, gets that one. Asks for the length also when the zone has it already but a length
    // asked for before has not come yet, so the last one asked wins. Throws like ResizeZone, and
    // then remembers nothing.
    public void ResizeForAWhile(string deviceId, int zoneIndex, int ledCount)
    {
        var remembered = false;
        lock (_gate)
        {
            var device = _lighting.Devices.FirstOrDefault(candidate => candidate.Id == deviceId);
            var zone = device is not null && zoneIndex >= 0 && zoneIndex < device.Zones.Count && IndexOf(device, device.Zones[zoneIndex].Name) == zoneIndex
                ? device.Zones[zoneIndex]
                : null;
            if (zone is null)
            {
                _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
                return;
            }

            var key = (deviceId, zone.Name);
            var wasAsked = _asked.TryGetValue(key, out var asked);
            if (zone.LedCount != ledCount || (wasAsked && asked != ledCount))
            {
                _asked[key] = ledCount;
            }

            try
            {
                _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
            }
            catch
            {
                if (wasAsked)
                {
                    _asked[key] = asked;
                }
                else
                {
                    _asked.Remove(key);
                }

                throw;
            }

            if (!_lengths.TryGetValue(deviceId, out var zones) || !zones.ContainsKey(zone.Name))
            {
                remembered = RememberLocked(deviceId, zone.Name, zone.LedCount);
            }
        }

        if (remembered)
        {
            LengthsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => _lighting.DevicesChanged -= OnDevicesChanged;

    // Returns true when it is another length than the one remembered.
    private bool RememberLocked(string deviceId, string zone, int ledCount)
    {
        if (!_lengths.TryGetValue(deviceId, out var zones))
        {
            zones = [];
            _lengths[deviceId] = zones;
        }

        var changed = !zones.TryGetValue(zone, out var remembered) || remembered != ledCount;
        zones[zone] = ledCount;
        return changed;
    }

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
        var changed = false;
        lock (_gate)
        {
            // Read under the lock, so a resize of the user that happens meanwhile is in the list or
            // in _asked, and the zone is not asked for the length it had before.
            var devices = _lighting.Devices;
            foreach (var key in _asked.Keys.Where(key => !devices.Any(device => device.Id == key.DeviceId)).ToList())
            {
                _asked.Remove(key);
            }

            foreach (var deviceId in _reported.Keys.Where(deviceId => !devices.Any(device => device.Id == deviceId)).ToList())
            {
                _reported.Remove(deviceId);
            }

            foreach (var device in devices)
            {
                if (_reported.TryGetValue(device.Id, out var before))
                {
                    changed |= FollowLocked(device, before);
                }
                else
                {
                    AskLocked(device, resizes);
                }

                _reported[device.Id] = ResizableZonesOf(device).ToDictionary(zone => zone.Zone.Name, zone => zone.Zone.LedCount);
            }

            ResizeLocked(resizes);
        }

        if (changed)
        {
            LengthsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ResizeLocked(List<(string DeviceId, int ZoneIndex, int LedCount)> resizes)
    {
        foreach (var (deviceId, zoneIndex, ledCount) in resizes)
        {
            try
            {
                _lighting.ResizeZone(deviceId, zoneIndex, ledCount);
            }
            catch (ArgumentException exception)
            {
                // The device went or changed after this list; it is asked again when it comes back.
                Trace.TraceWarning($"Could not give a zone of '{deviceId}' its length again: {exception.Message}");
            }
        }
    }

    private bool SameLengthsLocked(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> lengths) =>
        lengths.Count == _lengths.Count
        && lengths.All(device => _lengths.TryGetValue(device.Key, out var zones)
            && device.Value.Count == zones.Count
            && device.Value.All(zone => zones.TryGetValue(zone.Key, out var ledCount) && ledCount == zone.Value));

    // The resizable zones of the device that have a name of their own, with their index.
    private static IEnumerable<(RgbZone Zone, int Index)> ResizableZonesOf(RgbDevice device) =>
        device.Zones.Select((zone, index) => (Zone: zone, Index: index))
            .Where(zone => zone.Zone.Resizable && IndexOf(device, zone.Zone.Name) == zone.Index);

    // A device that shows up gets the lengths it does not have.
    private void AskLocked(RgbDevice device, List<(string, int, int)> resizes)
    {
        if (!_lengths.TryGetValue(device.Id, out var zones))
        {
            return;
        }

        foreach (var (name, ledCount) in zones)
        {
            var index = IndexOf(device, name);
            if (index < 0 || device.Zones[index].LedCount == ledCount)
            {
                continue;
            }

            var zone = device.Zones[index];
            if (zone.Resizable && zone.CanHold(ledCount))
            {
                _asked[(device.Id, name)] = ledCount;
                resizes.Add((device.Id, index, ledCount));
            }
            else
            {
                Trace.TraceWarning($"Zone '{name}' of '{device.Id}' holds {zone.MinLedCount} to {zone.MaxLedCount} LEDs now, not the {ledCount} it had; it keeps {zone.LedCount}.");
            }
        }
    }

    // A device that was there already: a length that was asked for is in once the device reports
    // it; another new length came from elsewhere and is remembered, except 0. Returns true when a
    // length was remembered.
    private bool FollowLocked(RgbDevice device, Dictionary<string, int> before)
    {
        var changed = false;
        foreach (var (zone, _) in ResizableZonesOf(device))
        {
            var key = (device.Id, zone.Name);
            if (_asked.TryGetValue(key, out var asked))
            {
                if (zone.LedCount == asked)
                {
                    _asked.Remove(key);
                }

                continue;
            }

            if (zone.LedCount == 0 || !before.TryGetValue(zone.Name, out var reported) || zone.LedCount == reported)
            {
                continue;
            }

            changed |= RememberLocked(device.Id, zone.Name, zone.LedCount);
        }

        return changed;
    }
}
