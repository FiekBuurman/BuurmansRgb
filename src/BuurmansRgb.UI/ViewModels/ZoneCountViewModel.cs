using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The count of the LEDs of one resizable zone as the engine runs it (IEngineApi
// .StartZoneCountAsync): a big counter ("LED 37"), Pauze/Doorgaan, − and + for one LED back or on,
// the pace, "Dit is de laatste" and "Stoppen". The engine tells each step (Show); what the user
// does shows at once and goes to the actions. − and + pause the count, so the LED stays where the
// user put it. One instance per count, kept while the preview is made again. Use it from the UI
// thread.
public sealed partial class ZoneCountViewModel : ObservableObject
{
    // The paces to choose from, in LEDs per second.
    public static IReadOnlyList<int> Paces { get; } = [1, 2, 5];

    private readonly RgbZone _zone;
    private readonly IPreviewActions? _actions;

    // zoneAccessibleName names the zone for a screen reader, such as "Moederbord – ARGB 1".
    public ZoneCountViewModel(ZoneCountInfo count, RgbZone zone, string zoneAccessibleName, IPreviewActions? actions)
    {
        DeviceId = count.DeviceId;
        ZoneIndex = count.ZoneIndex;
        _zone = zone;
        _actions = actions;
        ZoneAccessibleName = zoneAccessibleName;
        PaceOptions = [.. Paces.Select(pace => new ZoneCountPaceOption(this, pace))];
        Show(count);
    }

    public string DeviceId { get; }

    public int ZoneIndex { get; }

    public string ZoneAccessibleName { get; }

    // The LED that burns green, from 1.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LedText), nameof(AccessibleName))]
    [NotifyCanExecuteChangedFor(nameof(KeepCommand))]
    public partial int Led { get; private set; }

    // How many LEDs the count goes up to: the longest the zone can be.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial int LedCount { get; private set; }

    [ObservableProperty]
    public partial int LedsPerSecond { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseOrResumeText))]
    public partial bool Running { get; private set; }

    public IReadOnlyList<ZoneCountPaceOption> PaceOptions { get; }

    // The big counter.
    public string LedText => UiText.Format(Strings.ZoneCountLed, Led);

    // What a screen reader calls the counter, with the zone and the end of the count.
    public string AccessibleName => UiText.Format(Strings.ZoneCountAccessibleName, ZoneAccessibleName, Led, LedCount);

    public string PauseOrResumeText => Running ? Strings.ZoneCountPause : Strings.ZoneCountResume;

    // What the count does and what to do next. It changes when the count pauses, goes on, gets
    // another pace or another LED while paused, not at each step, so a screen reader that reads it
    // as a live region is not flooded.
    [ObservableProperty]
    public partial string Status { get; private set; } = string.Empty;

    // Lets the controls keep the focus when the preview is made again; the view adds the role.
    public string FocusKey => $"zone-count/{DeviceId}/{ZoneIndex}";

    public bool Counts(string deviceId, int zoneIndex) => DeviceId == deviceId && ZoneIndex == zoneIndex;

    // The state from the engine.
    public void Show(ZoneCountInfo count)
    {
        LedCount = count.LedCount;
        Led = count.Led;
        LedsPerSecond = count.LedsPerSecond;
        Running = count.Running;
        Refresh();
    }

    internal void ChoosePace(int ledsPerSecond)
    {
        if (ledsPerSecond == LedsPerSecond)
        {
            return;
        }

        LedsPerSecond = ledsPerSecond;
        Refresh();
        Send(new ZoneCountChange(LedsPerSecond: ledsPerSecond));
    }

    // The status and the chosen pace; a status that stays the same raises no change.
    private void Refresh()
    {
        Status = Running ? UiText.Format(LedsPerSecond == 1 ? Strings.ZoneCountRunningOne : Strings.ZoneCountRunning, LedsPerSecond)
            : Led >= LedCount ? UiText.Format(Strings.ZoneCountAtEnd, LedCount)
            : UiText.Format(Strings.ZoneCountPaused, Led, LedCount);
        foreach (var option in PaceOptions)
        {
            option.Refresh();
        }
    }

    // At the last LED there is nothing to go on with, so it starts again from LED 1.
    [RelayCommand]
    private void PauseOrResume()
    {
        if (Running)
        {
            // The LED the user sees when choosing Pauze is where it stays, also when the engine
            // just went one further.
            Running = false;
            Refresh();
            Send(new ZoneCountChange(Led: Led, Running: false));
        }
        else if (Led >= LedCount)
        {
            Led = 1;
            Running = true;
            Refresh();
            Send(new ZoneCountChange(Led: 1, Running: true));
        }
        else
        {
            Running = true;
            Refresh();
            Send(new ZoneCountChange(Running: true));
        }
    }

    // − and + stay enabled at the ends, as those of a zone length, and then only pause.
    [RelayCommand]
    private void Previous() => GoTo(Math.Max(Led - 1, 1));

    [RelayCommand]
    private void Next() => GoTo(Math.Min(Led + 1, LedCount));

    // The zone gets as many LEDs as the counter says, and the engine ends the count with it.
    [RelayCommand(CanExecute = nameof(CanKeep))]
    private void Keep() => _actions?.ResizeZone(new ZoneResize(DeviceId, ZoneIndex, Led));

    private bool CanKeep() => _zone.CanHold(Led);

    [RelayCommand]
    private void Stop() => _actions?.StopZoneCount(new ZoneCountStop(DeviceId, ZoneIndex));

    private void GoTo(int led)
    {
        Led = led;
        Running = false;
        Refresh();
        Send(new ZoneCountChange(Led: led, Running: false));
    }

    private void Send(ZoneCountChange change) => _actions?.ChangeZoneCount(new ZoneCountUpdate(DeviceId, ZoneIndex, change));
}
