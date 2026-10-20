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
    private Func<string, Task<LightingInfo>>? _getLighting;

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

    // The big switch is on while any lamp is on. Switched off, every lamp goes off; switched on,
    // every lamp goes on.
    public bool IsAnyOn
    {
        get => OnCount > 0;
        set
        {
            if (value == IsAnyOn || Lamps.Count == 0)
            {
                return;
            }

            foreach (var lamp in Lamps)
            {
                lamp.Show(value);
            }

            OnLampsChanged();
            SwitchRequested?.Invoke(this, new LampSwitch(null, value));
        }
    }

    // The devices of the list, without "all devices". A lamp that stays keeps what it showed until
    // the next refresh.
    public void ShowDevices(IEnumerable<DeviceItem> devices)
    {
        var old = Lamps.ToDictionary(lamp => lamp.DeviceId);
        foreach (var lamp in Lamps)
        {
            lamp.SwitchRequested -= OnLampSwitchRequested;
            lamp.PropertyChanged -= OnLampChanged;
        }

        Lamps.Clear();
        foreach (var device in devices.Where(device => device.DeviceId is not null))
        {
            var lamp = new LampItem(device);
            if (old.TryGetValue(lamp.DeviceId, out var previous))
            {
                lamp.Show(previous.IsOn);
            }

            lamp.SwitchRequested += OnLampSwitchRequested;
            lamp.PropertyChanged += OnLampChanged;
            Lamps.Add(lamp);
        }

        OnLampsChanged();
        OnPropertyChanged(nameof(HasLamps));
    }

    // Asks the lighting of every lamp. A refresh asked while one runs makes it go round once more
    // when it is done, with the getLighting of the last call, so the last change always shows and two
    // refreshes never interleave.
    public async Task RefreshAsync(Func<string, Task<LightingInfo>> getLighting)
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
                    foreach (var lamp in Lamps.ToArray())
                    {
                        lamp.Show(await _getLighting(lamp.DeviceId));
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
