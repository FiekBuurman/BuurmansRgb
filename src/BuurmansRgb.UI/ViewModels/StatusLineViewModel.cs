using System.Collections.ObjectModel;
using System.ComponentModel;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The status line at the top of Apparaten and Kleur (Statusregel, design/DESIGN.md § 4): at most
// one message in view, the one that matters most, with its action, and "Details" that unfolds the
// others that hold, each with its own action, so a message that is pushed aside never goes
// without a word. In the order they matter: the connection with the devices gone
// (OpenRgbStatus.IsConnectionLost; "Opnieuw verbinden" is the search of Verversen) > other RGB
// software that runs, one message per program in the order of the engine (OtherRgbSoftware;
// "Uitleg" that unfolds what to do under the message, and "Niet meer melden") > the effects
// paused or just resumed (EffectsPause; "Hervatten" for the pause from the tray menu) > the
// preview still because animations in Windows are off (Motion; "Laten lopen" or "Stilzetten").
// It follows those view models and keeps their texts; they do the work.
public sealed partial class StatusLineViewModel : ObservableObject, IDisposable
{
    private readonly OpenRgbStatusViewModel _openRgb;
    private readonly EffectsPauseViewModel _pause;
    private readonly PreviewMotionViewModel _motion;
    private readonly DeviceRescanViewModel _rescan;
    private readonly StatusMessage _connectionLost;
    private readonly StatusMessage _paused;
    private readonly StatusMessage _previewStill;
    private readonly OtherRgbSoftwareViewModel? _otherRgbSoftware;

    // One message per program, made when it first shows and kept, so its item under "Details"
    // stays the same when the list changes around it.
    private readonly Dictionary<string, StatusMessage> _otherRgbSoftwareMessages = new(StringComparer.Ordinal);

    // otherRgbSoftware is null in tests that leave it out.
    public StatusLineViewModel(OpenRgbStatusViewModel openRgb, EffectsPauseViewModel pause, PreviewMotionViewModel motion, DeviceRescanViewModel rescan, OtherRgbSoftwareViewModel? otherRgbSoftware = null)
    {
        _openRgb = openRgb;
        _pause = pause;
        _motion = motion;
        _rescan = rescan;
        _otherRgbSoftware = otherRgbSoftware;
        _connectionLost = new StatusMessage(StatusMessageKind.ConnectionLost, rescan.RescanCommand);
        _paused = new StatusMessage(StatusMessageKind.Paused, pause.ResumeCommand, actionEndsMessage: true);
        _previewStill = new StatusMessage(StatusMessageKind.PreviewStill, motion.ToggleCommand);
        _openRgb.PropertyChanged += OnSourceChanged;
        _pause.PropertyChanged += OnSourceChanged;
        _motion.PropertyChanged += OnSourceChanged;
        _rescan.PropertyChanged += OnSourceChanged;
        _otherRgbSoftware?.PropertyChanged += OnSourceChanged;
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
        _otherRgbSoftware?.PropertyChanged -= OnSourceChanged;
    }

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    // The sources change one property at a time; this reads them whole, so the line is consistent
    // after each. Others changes only when the set or the order changed, so the list under
    // "Details" is not made anew for a text that changed. The messages that hold get their texts
    // first. A message that leaves from under "Details" is emptied before Others lets go of its
    // item: its button hides while it is still in the tree, so the keyboard focus on it can be
    // handed on (StatusLine.OnButtonIsVisibleChanged). The message that leaves the line is emptied
    // last, once the line moved on: so its button hides only when it is no longer in the line, and
    // keeps the focus when another message takes the line with its own action.
    private void Refresh()
    {
        var lost = _openRgb.IsConnectionLost;
        var paused = _pause.IsShown;
        var still = _motion.IsShown;
        if (lost)
        {
            _connectionLost.Show(Strings.StatusConnectionLost, _rescan.ReconnectButtonText, _rescan.ReconnectButtonText, Strings.ReconnectToolTip);
        }

        if (paused)
        {
            _paused.Show(_pause.Text, _pause.CanResume ? Strings.ResumeEffects : null, Strings.ResumeEffectsAccessibleName, Strings.ResumeEffectsToolTip);
        }

        if (still)
        {
            _previewStill.Show(_motion.Text, _motion.ButtonText, _motion.ButtonAccessibleName, _motion.ButtonHelpText);
        }

        var otherRgbSoftware = ShowOtherRgbSoftware();
        StatusMessage[] active =
        [
            .. new[] { (_connectionLost, lost) }.Where(pair => pair.Item2).Select(pair => pair.Item1),
            .. otherRgbSoftware,
            .. new[] { (_paused, paused), (_previewStill, still) }.Where(pair => pair.Item2).Select(pair => pair.Item1),
        ];
        var current = active.FirstOrDefault();
        var others = active.Skip(1).ToList();
        var previous = Current;
        foreach (var message in new[] { _connectionLost, _paused, _previewStill }.Concat(_otherRgbSoftwareMessages.Values).Except(active))
        {
            if (!ReferenceEquals(message, previous))
            {
                message.Hide();
            }
        }

        if (!others.SequenceEqual(Others))
        {
            UpdateOthers(others);
            OnPropertyChanged(nameof(HasDetails));
            OnPropertyChanged(nameof(DetailsAccessibleName));
            if (others.Count == 0)
            {
                ShowsDetails = false;
            }
        }

        Current = current;

        if (previous is not null && !active.Contains(previous))
        {
            previous.Hide();
        }
    }

    // The messages of the other RGB software that runs, with their texts, in the engine's order.
    private List<StatusMessage> ShowOtherRgbSoftware()
    {
        var messages = new List<StatusMessage>();
        if (_otherRgbSoftware is not { } source)
        {
            return messages;
        }

        foreach (var software in source.Programs)
        {
            if (!_otherRgbSoftwareMessages.TryGetValue(software.Id, out var message))
            {
                message = new StatusMessage(
                    StatusMessageKind.OtherRgbSoftware,
                    dismissCommand: new RelayCommand(() => source.DismissCommand.Execute(software.Id)),
                    explanationLinkCommand: source.OpenGuideCommand);
                _otherRgbSoftwareMessages.Add(software.Id, message);
            }

            var name = OtherRgbSoftwareViewModel.NameFor(software);
            message.Show(OtherRgbSoftwareViewModel.TextFor(software), null, null, null);
            message.ShowExplanation(
                OtherRgbSoftwareViewModel.ExplanationFor(software),
                UiText.Format(Strings.OtherRgbSoftwareExplanationAccessibleName, name),
                source.HasGuide ? Strings.OtherRgbSoftwareGuideLink : null,
                Strings.OtherRgbSoftwareGuideLinkAccessibleName);
            message.ShowDismiss(Strings.OtherRgbSoftwareDismiss, UiText.Format(Strings.OtherRgbSoftwareDismissAccessibleName, name), Strings.OtherRgbSoftwareDismissToolTip);
            messages.Add(message);
        }

        return messages;
    }

    // One message at a time, not Clear and Add: the list under "Details" keeps the item of a
    // message that stays, and with it the keyboard focus on its button when another message
    // leaves (the connection back while the focus is on "Laten lopen").
    private void UpdateOthers(List<StatusMessage> others)
    {
        for (var i = Others.Count - 1; i >= 0; i--)
        {
            if (!others.Contains(Others[i]))
            {
                Others.RemoveAt(i);
            }
        }

        for (var i = 0; i < others.Count; i++)
        {
            var index = Others.IndexOf(others[i]);
            if (index < 0)
            {
                Others.Insert(i, others[i]);
            }
            else if (index != i)
            {
                Others.Move(index, i);
            }
        }
    }
}
