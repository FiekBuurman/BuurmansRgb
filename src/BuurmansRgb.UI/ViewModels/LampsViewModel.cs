using System.Collections.ObjectModel;
using System.ComponentModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The Lampen page: every device as a lamp that is on or off, the whole house with how many are on,
// and a strip with what the lamps show now. This only asks; MainViewModel sends a switch to the
// engine and asks the lighting of the lamps again with RefreshAsync.
public sealed partial class LampsViewModel : ObservableObject
{
    public const int StripLength = 14;

    private bool _refreshing;
    private bool _refreshAgain;

    // The way to ask the lighting that the last RefreshAsync gave, such as of a new connection.
    private Func<Task<IReadOnlyList<DeviceLightingInfo>>>? _getLighting;

    // The sequence of the last frame shown; frames can overtake each other.
    private long _sequence;

    public LampsViewModel() => Strip = [.. Enumerable.Range(0, StripLength).Select(_ => new StripLed())];

    public event EventHandler<LampSwitch>? SwitchRequested;

    // Raised with the device id when the user opens a lamp, to change its color.
    public event EventHandler<string>? OpenRequested;

    // In the order of the device list: the real lamps first, then the simulated ones.
    public ObservableCollection<LampItem> Lamps { get; } = [];

    public IReadOnlyList<StripLed> Strip { get; }

    public int OnCount => Lamps.Count(lamp => lamp.IsOn);

    // "3 van 4 aan".
    public string Summary => UiText.Format(Strings.WholeHouseOnCount, OnCount, Lamps.Count);

    public bool HasLamps => Lamps.Count > 0;

    // Every lamp is a simulated one: OpenRGB gave no real device, or none yet.
    public bool HasOnlySimulatedLamps => HasLamps && Lamps.All(lamp => lamp.Device.IsSimulated);

    // The big switch is on while any lamp is on. Switched off, every lamp goes off; switched on,
    // every lamp goes on.
    public bool IsAnyOn
    {
        get => OnCount > 0;
        set
        {
            if (value != IsAnyOn && Lamps.Count > 0)
            {
                SwitchAll(value);
            }
        }
    }

    // Switches the lamp on as if the user did, when it is off; with null every lamp, when any is
    // off. A lamp that is on, and every other lamp, stays as it is.
    public void SwitchOn(string? deviceId)
    {
        if (deviceId is null)
        {
            if (Lamps.Any(lamp => !lamp.IsOn))
            {
                SwitchAll(true);
            }
        }
        else if (Lamps.FirstOrDefault(lamp => lamp.DeviceId == deviceId) is { IsOn: false } lamp)
        {
            lamp.IsOn = true;
        }
    }

    // The devices of the list, without "all devices". A lamp whose device stays the same stays the same
    // item, so its card keeps the keyboard focus; one whose device changed (such as another location
    // label) keeps what it showed until the next refresh. The list changes item by item, not all at once.
    public void ShowDevices(IEnumerable<DeviceItem> devices)
    {
        var old = Lamps.ToDictionary(lamp => lamp.DeviceId);
        List<LampItem> lamps = [];
        foreach (var device in devices.Where(device => device.DeviceId is not null))
        {
            if (old.TryGetValue(device.DeviceId!, out var previous) && previous.Device == device)
            {
                lamps.Add(previous);
                continue;
            }

            var lamp = new LampItem(device);
            if (previous is not null)
            {
                lamp.ShowAs(previous);
            }

            lamp.SwitchRequested += OnLampSwitchRequested;
            lamp.PropertyChanged += OnLampChanged;
            lamps.Add(lamp);
        }

        for (var i = 0; i < lamps.Count; i++)
        {
            var at = Lamps.IndexOf(lamps[i]);
            if (at < 0)
            {
                Lamps.Insert(i, lamps[i]);
            }
            else if (at != i)
            {
                Lamps.Move(at, i);
            }
        }

        while (Lamps.Count > lamps.Count)
        {
            var gone = Lamps[^1];
            gone.SwitchRequested -= OnLampSwitchRequested;
            gone.PropertyChanged -= OnLampChanged;
            Lamps.RemoveAt(Lamps.Count - 1);
        }

        OnLampsChanged();
        OnPropertyChanged(nameof(HasLamps));
        OnPropertyChanged(nameof(HasOnlySimulatedLamps));
    }

    // Asks the lighting of all lamps in one go. A refresh asked while one runs makes it go round once
    // more when it is done, with the getLighting of the last call, so the last change always shows and
    // two refreshes never interleave. A device the answer does not have, or a lamp that is gone by
    // then, is left as it is.
    public async Task RefreshAsync(Func<Task<IReadOnlyList<DeviceLightingInfo>>> getLighting)
    {
        _getLighting = getLighting;
        if (_refreshing)
        {
            _refreshAgain = true;
            return;
        }

        _refreshing = true;
        try
        {
            do
            {
                _refreshAgain = false;
                try
                {
                    foreach (var device in await _getLighting())
                    {
                        Lamps.FirstOrDefault(lamp => lamp.DeviceId == device.DeviceId)?.Show(device.Lighting);
                    }
                }
                catch when (_refreshAgain)
                {
                    // A newer refresh was asked meanwhile, such as on a new connection after this one
                    // was lost; that one goes on.
                }
            }
            while (_refreshAgain);
        }
        finally
        {
            _refreshing = false;
        }
    }

    // Forgets the frames of an earlier connection, whose sequence numbers do not apply.
    public void Reset() => _sequence = 0;

    // The colors of every lamp, and the strip spread evenly over all their LEDs one after the other.
    public void ShowFrame(LightingFrame frame)
    {
        if (frame.Sequence < _sequence)
        {
            return;
        }

        _sequence = frame.Sequence;
        var total = 0;
        List<(LampItem Lamp, byte[] Leds)> lit = [];
        foreach (var lamp in Lamps)
        {
            if (frame.Devices.FirstOrDefault(device => device.DeviceId == lamp.DeviceId) is { } device)
            {
                lamp.ShowLeds(device.Leds);
                lit.Add((lamp, device.Leds));
                total += device.Leds.Length / 3;
            }
        }

        // A frame with only some of the lamps (Kleur asks for the one it shows) would squeeze the
        // strip onto those; it stays as it was until a frame with all of them comes.
        if (lit.Count < Lamps.Count)
        {
            return;
        }

        for (var i = 0; i < Strip.Count; i++)
        {
            Strip[i].IsLit = false;
            var led = (int)((i + 0.5) * total / Strip.Count);
            foreach (var (lamp, leds) in lit)
            {
                var count = leds.Length / 3;
                if (led < count)
                {
                    Strip[i].Color = new RgbColor(leds[led * 3], leds[led * 3 + 1], leds[led * 3 + 2]);
                    Strip[i].IsLit = lamp.IsOn;
                    break;
                }

                led -= count;
            }
        }
    }

    // One call for the whole house, as the engine switches every device with it.
    private void SwitchAll(bool on)
    {
        foreach (var lamp in Lamps)
        {
            lamp.Show(on);
        }

        OnLampsChanged();
        SwitchRequested?.Invoke(this, new LampSwitch(null, on));
    }

    [RelayCommand]
    private void Open(LampItem lamp) => OpenRequested?.Invoke(this, lamp.DeviceId);

    private void OnLampSwitchRequested(object? sender, bool on) =>
        SwitchRequested?.Invoke(this, new LampSwitch(((LampItem)sender!).DeviceId, on));

    private void OnLampChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LampItem.IsOn))
        {
            OnLampsChanged();
        }
    }

    private void OnLampsChanged()
    {
        OnPropertyChanged(nameof(OnCount));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsAnyOn));
    }
}
