using System.Collections.ObjectModel;
using System.ComponentModel;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// "Apparaten die meedoen" of the selected scene: "Alle apparaten", which also takes a device that
// comes later, or a check box per device. A change is saved at once, as the rest of the app; only
// one without a device is not, as that scene would do nothing (Error). This only asks;
// MainViewModel sends the change to the engine and shows what the engine saved.
public sealed partial class SceneDevicesViewModel : ObservableObject
{
    // The devices there are now, without "Alle apparaten", in the order of the device list.
    private IReadOnlyList<DeviceItem> _devices = [];

    // What the engine has, or will have once it answers: null for every device.
    private IReadOnlyList<string>? _saved;

    // While Display fills in the boxes.
    private bool _displaying;

    public event EventHandler<SceneDevicesChange>? ApplyRequested;

    // A box per device there is now; while "Alle apparaten" is on, all checked and grey.
    public ObservableCollection<SceneDeviceOption> Options { get; } = [];

    // The scene it is about; null while no scene is selected.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChoose), nameof(CanPick), nameof(AllOffNote))]
    public partial string? ProfileName { get; private set; }

    // "Alles uit" switches every device off for "Uit om", so there is no choice (AllOffNote).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChoose), nameof(CanPick), nameof(AllOffNote))]
    public partial bool IsAllOff { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPick))]
    public partial bool AllDevices { get; set; } = true;

    public bool CanChoose => ProfileName is not null && !IsAllOff;

    // Whether the boxes of the devices can change.
    public bool CanPick => CanChoose && !AllDevices;

    // Why "Alles uit" has no choice; null for another scene.
    public string? AllOffNote => IsAllOff && ProfileName is { } name ? UiText.Format(Strings.SceneDevicesAllOff, name) : null;

    // The devices of the scene that are not there now, such as a headset that is off; they stay in
    // the scene. Null without them.
    [ObservableProperty]
    public partial string? AwayNote { get; private set; }

    // Says so when no device is checked. Only raised when it really changes, so a screen reader
    // hears it once.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    // The devices there are now, as on Apparaten (DeviceItem, without "Alle apparaten"); the boxes
    // show what the engine saved for them.
    public void ShowDevices(IEnumerable<DeviceItem> devices)
    {
        _devices = [.. devices.Where(device => device.DeviceId is not null)];
        Display(ProfileName, _saved, IsAllOff);
    }

    // What the engine saved for the scene; isAllOff for "Alles uit" (WholeHouseTimes.OffScene).
    public void Show(string profileName, IReadOnlyList<string>? deviceIds, bool isAllOff) => Display(profileName, deviceIds, isAllOff);

    // "Alles uit" became another scene, such as after restoring the settings: the same scene and
    // devices, with or without a choice.
    public void ShowIsAllOff(bool isAllOff)
    {
        if (ProfileName is not null && isAllOff != IsAllOff)
        {
            Display(ProfileName, _saved, isAllOff);
        }
    }

    public void Clear() => Display(null, null, isAllOff: false);

    private void Display(string? profileName, IReadOnlyList<string>? deviceIds, bool isAllOff)
    {
        _displaying = true;
        try
        {
            _saved = deviceIds;
            ProfileName = profileName;
            IsAllOff = isAllOff;
            AllDevices = deviceIds is null;
            ShowOptions(deviceIds);

            var away = AwayIds().Count;
            AwayNote = away == 0 ? null : UiText.Format(away == 1 ? Strings.SceneDevicesAway : Strings.SceneDevicesAwayMore, away);
            Error = null;
        }
        finally
        {
            _displaying = false;
        }
    }

    // The same devices as before keep their boxes, so the box that has the focus keeps it after its
    // change is saved; other devices get new ones.
    private void ShowOptions(IReadOnlyList<string>? deviceIds)
    {
        if (!Options.Select(option => (option.DeviceId, option.Name)).SequenceEqual(_devices.Select(device => (device.DeviceId!, device.DisplayName))))
        {
            foreach (var option in Options)
            {
                option.PropertyChanged -= OnOptionChanged;
            }

            Options.Clear();
            foreach (var device in _devices)
            {
                var option = new SceneDeviceOption(device.DeviceId!, device.DisplayName);
                option.PropertyChanged += OnOptionChanged;
                Options.Add(option);
            }
        }

        foreach (var option in Options)
        {
            option.IsChecked = deviceIds?.Contains(option.DeviceId) ?? true;
        }
    }

    // On: every device, also one that comes later. Off: the devices there are now, each checked,
    // so the scene stays as it was until a box goes off.
    partial void OnAllDevicesChanged(bool value)
    {
        if (_displaying)
        {
            return;
        }

        _displaying = true;
        try
        {
            foreach (var option in Options)
            {
                option.IsChecked = true;
            }
        }
        finally
        {
            _displaying = false;
        }

        Apply();
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_displaying && e.PropertyName == nameof(SceneDeviceOption.IsChecked))
        {
            Apply();
        }
    }

    // The checked devices in the order of the list, and after them those of the scene that are
    // not there now. Without any device nothing is saved: the scene would do nothing.
    private void Apply()
    {
        if (!CanChoose)
        {
            return;
        }

        if (!AllDevices && !Options.Any(option => option.IsChecked) && AwayIds().Count == 0)
        {
            Error = Strings.SceneDevicesNone;
            return;
        }

        Error = null;
        IReadOnlyList<string>? deviceIds = AllDevices
            ? null
            : SceneDevices.Normalize([.. Options.Where(option => option.IsChecked).Select(option => option.DeviceId), .. AwayIds()]);
        if (deviceIds is null ? _saved is null : _saved is not null && deviceIds.SequenceEqual(_saved))
        {
            return;
        }

        // Taken as saved now, so a box that goes off and on again before the engine answers sends
        // both changes, not only the first. When the engine refuses it, MainViewModel shows what the
        // engine has again.
        _saved = deviceIds;
        ApplyRequested?.Invoke(this, new SceneDevicesChange(ProfileName!, deviceIds));
    }

    private List<string> AwayIds() =>
        _saved is null ? [] : [.. _saved.Where(id => !_devices.Any(device => device.DeviceId == id))];
}
