using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// "Uit om" and "Aan om" in the block Hele huis on Apparaten: every evening off at a time, and on
// again, without a scene to make first. A change counts at once, as the switches of the app: the
// switch, a day, or the time once the field loses the focus or gets Enter. This only asks;
// MainViewModel sends it to the engine, which keeps "Uit om" as the times of the scene "Alles uit"
// (so Scènes shows them too) and "Aan om" apart, and shows what the engine saved. A scene with
// more times shows the first here; the rest stays as it is.
public sealed partial class WholeHouseTimesViewModel : ObservableObject
{
    // What the engine has, so only a change is sent, and the times after the first stay.
    private WholeHouseTimes _saved = WholeHouseTimes.None;

    public WholeHouseTimesViewModel()
    {
        Off = new WholeHouseTimeRow(Strings.WholeHouseOff, Strings.WholeHouseOffHint, new TimeOnly(23, 0));
        On = new WholeHouseTimeRow(Strings.WholeHouseOn, Strings.WholeHouseOnHint, new TimeOnly(7, 0));
        Off.Changed += OnRowChanged;
        On.Changed += OnRowChanged;
    }

    public event EventHandler<WholeHouseTimes>? ApplyRequested;

    public WholeHouseTimeRow Off { get; }

    public WholeHouseTimeRow On { get; }

    // Only once the engine said what it has, so a switch does not send what it does not know.
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    // What the engine saved last.
    public WholeHouseTimes Saved => _saved;

    // The scene "Alles uit" whose times "Uit om" are, or null.
    public string? OffScene => _saved.OffScene;

    // That "Uit om" puts on that scene, and so shows on Scènes too; with more times than the first,
    // that the rest is there.
    public string? SceneNote => _saved.OffScene is not { } scene ? null
        : _saved.Off.Count > 1 ? UiText.Format(Strings.WholeHouseSceneMoreTimes, scene, _saved.Off.Count - 1)
        : UiText.Format(Strings.WholeHouseSceneNote, scene);

    public bool HasSceneNote => SceneNote is not null;

    // What is wrong with a row that is on, or null. Only raised when it really changes, so a screen
    // reader hears it once.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    public bool HasError => Error is not null;

    // Whether a time is on, for "Na een herstart".
    public bool HasTimes => _saved.Off.Count > 0 || _saved.On.Count > 0;

    // "Uit om 23:00 (elke dag) en aan om 07:00 (werkdagen)", or one of them; null without times.
    public string? Summary
    {
        get
        {
            var off = _saved.Off.Count > 0 ? UiText.Format(Strings.WholeHouseSummaryOff, Describe(_saved.Off[0])) : null;
            var on = _saved.On.Count > 0 ? UiText.Format(Strings.WholeHouseSummaryOn, Describe(_saved.On[0])) : null;
            return off is not null && on is not null ? UiText.Format(Strings.WholeHouseSummaryBoth, off, on) : off ?? on;
        }
    }

    // What the engine saved.
    public void Show(WholeHouseTimes times)
    {
        _saved = times;
        Off.Show(times.Off.FirstOrDefault());
        On.Show(times.On.FirstOrDefault());
        IsAvailable = true;
        Error = null;
        OnPropertyChanged(nameof(OffScene));
        OnPropertyChanged(nameof(SceneNote));
        OnPropertyChanged(nameof(HasSceneNote));
        OnPropertyChanged(nameof(HasTimes));
        OnPropertyChanged(nameof(Summary));
    }

    // Without an engine nothing can be set.
    public void Clear()
    {
        IsAvailable = false;
        Error = null;
    }

    private static string Describe(LoadTime time) =>
        UiText.Format(Strings.WholeHouseSummaryTime, time.At.ToString("HH:mm", CultureInfo.InvariantCulture), UiText.Days(time.Days).ToLower(CultureInfo.CurrentCulture));

    private void OnRowChanged(object? sender, EventArgs e)
    {
        if (!IsAvailable)
        {
            return;
        }

        Error = FindError(Off) ?? FindError(On);
        if (Error is not null || !Off.TryRead(out var off) || !On.TryRead(out var on))
        {
            return;
        }

        var times = new WholeHouseTimes(Replace(_saved.Off, off), Replace(_saved.On, on), _saved.OffScene);
        if (times != _saved)
        {
            ApplyRequested?.Invoke(this, times);
        }
    }

    // The first time is the one of the row; off, there is none, also not the rest.
    private static IReadOnlyList<LoadTime> Replace(IReadOnlyList<LoadTime> saved, LoadTime? first) =>
        first is null ? [] : [first, .. saved.Skip(1)];

    private static string? FindError(WholeHouseTimeRow row) =>
        !row.IsOn ? null
        : !row.HasValidTime ? UiText.Format(Strings.WholeHouseTimeInvalid, row.Label)
        : row.CheckedDays == WeekDays.None ? UiText.Format(Strings.WholeHouseDaysNone, row.Label)
        : null;
}
