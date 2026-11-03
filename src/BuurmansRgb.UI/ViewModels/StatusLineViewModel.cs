using System.Collections.ObjectModel;
using System.ComponentModel;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The status line at the top of Apparaten and Kleur (Statusregel, design/DESIGN.md § 4): at most
// one message in view, the one that matters most, with its action, and "Details" that unfolds the
// others that hold, each with its own action, so a message that is pushed aside never goes
// without a word. In the order they matter: the connection with the devices gone (OpenRgbStatus
// out of reach, once this pc had real devices (DevicesExpected); "Opnieuw verbinden" is the search
// of Verversen) > the effects paused or just resumed (EffectsPause; "Hervatten" for the pause from
// the tray menu) > the preview still because animations in Windows are off (Motion; "Laten lopen"
// or "Stilzetten"). It follows those view models and keeps their texts; they do the work.
public sealed partial class StatusLineViewModel : ObservableObject, IDisposable
{
    private readonly OpenRgbStatusViewModel _openRgb;
    private readonly EffectsPauseViewModel _pause;
    private readonly PreviewMotionViewModel _motion;
    private readonly DeviceRescanViewModel _rescan;
    private readonly StatusMessage _connectionLost;
    private readonly StatusMessage _paused;
    private readonly StatusMessage _previewStill;

    public StatusLineViewModel(OpenRgbStatusViewModel openRgb, EffectsPauseViewModel pause, PreviewMotionViewModel motion, DeviceRescanViewModel rescan)
    {
        _openRgb = openRgb;
        _pause = pause;
        _motion = motion;
        _rescan = rescan;
        _connectionLost = new StatusMessage(StatusMessageKind.ConnectionLost, rescan.RescanCommand);
        _paused = new StatusMessage(StatusMessageKind.Paused, pause.ResumeCommand, actionEndsMessage: true);
        _previewStill = new StatusMessage(StatusMessageKind.PreviewStill, motion.ToggleCommand);
        _openRgb.PropertyChanged += OnSourceChanged;
        _pause.PropertyChanged += OnSourceChanged;
        _motion.PropertyChanged += OnSourceChanged;
        _rescan.PropertyChanged += OnSourceChanged;
        Refresh();
    }

    // The message in the line; null while there is nothing to say.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown))]
    public partial StatusMessage? Current { get; private set; }

    // The other messages that hold, pushed aside by Current, in the order they matter.
    public ObservableCollection<StatusMessage> Others { get; } = [];

    // "Details" open: the others show under the line. Closes by itself once there are none.
    [ObservableProperty]
    public partial bool ShowsDetails { get; set; }

    // Whether a real device was ever in the list on this pc (MainViewModel.HadRealDevices): only
    // then is OpenRGB out of reach a lost connection; without, the simulated devices are all there
    // is, and the status at the top right says enough.
    [ObservableProperty]
    public partial bool DevicesExpected { get; set; }

    public bool IsShown => Current is not null;

    public bool HasDetails => Others.Count > 0;

    // "Details: nog 1 melding", so a screen reader hears what the button holds.
    public string DetailsAccessibleName =>
        Others.Count == 1 ? Strings.StatusDetailsAccessibleNameOne : UiText.Format(Strings.StatusDetailsAccessibleName, Others.Count);

    public void Dispose()
    {
        _openRgb.PropertyChanged -= OnSourceChanged;
        _pause.PropertyChanged -= OnSourceChanged;
        _motion.PropertyChanged -= OnSourceChanged;
        _rescan.PropertyChanged -= OnSourceChanged;
    }

    partial void OnDevicesExpectedChanged(bool value) => Refresh();

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    // The sources change one property at a time; this reads them whole, so the line is consistent
    // after each. Others changes only when the set or the order changed, so the list under
    // "Details" is not made anew for a text that changed.
    private void Refresh()
    {
        var lost = DevicesExpected && _openRgb.IsDisconnected;
        _connectionLost.Show(lost ? Strings.StatusConnectionLost : null, _rescan.ReconnectButtonText, _rescan.ReconnectButtonText, Strings.ReconnectToolTip);
        _paused.Show(_pause.IsShown ? _pause.Text : null, _pause.CanResume ? Strings.ResumeEffects : null, Strings.ResumeEffectsAccessibleName, Strings.ResumeEffectsToolTip);
        _previewStill.Show(_motion.IsShown ? _motion.Text : null, _motion.ButtonText, _motion.ButtonAccessibleName, _motion.ButtonHelpText);

        StatusMessage[] active = [.. new[] { _connectionLost, _paused, _previewStill }.Where(message => message.IsActive)];
        var current = active.FirstOrDefault();
        var others = active.Skip(1).ToList();
        if (!others.SequenceEqual(Others))
        {
            Others.Clear();
            foreach (var message in others)
            {
                Others.Add(message);
            }

            OnPropertyChanged(nameof(HasDetails));
            OnPropertyChanged(nameof(DetailsAccessibleName));
            if (others.Count == 0)
            {
                ShowsDetails = false;
            }
        }

        Current = current;
    }
}
