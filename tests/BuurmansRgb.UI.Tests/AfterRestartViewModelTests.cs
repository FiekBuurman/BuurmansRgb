using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// "Na een herstart" on Instellingen follows the scene that is on, the scene times, the sleep choice
// and "Starten met Windows" as they change.
public sealed class AfterRestartViewModelTests
{
    private readonly ProfilesViewModel _profiles = new();
    private readonly EffectOptionsViewModel _effectOptions = new();
    private readonly SwitchOptionViewModel _autoStart = new(isOn: false);
    private readonly SwitchOptionViewModel _sceneTimes = new(isOn: false);
    private readonly WholeHouseTimesViewModel _wholeHouse = new();
    private readonly List<string?> _changed = [];
    private readonly AfterRestartViewModel _afterRestart;

    public AfterRestartViewModelTests()
    {
        _afterRestart = new(_profiles, _effectOptions, _autoStart, _sceneTimes, _wholeHouse);
        _afterRestart.PropertyChanged += (_, e) => _changed.Add(e.PropertyName);
    }

    [Fact]
    public void WithoutAScene_KeepsTheLightingOfEveryDevice() =>
        Assert.Equal("de kleur en het effect van elk apparaat, en of het aan of uit staat", _afterRestart.LightingText);

    [Fact]
    public void SceneOnAndOff_FollowsIt()
    {
        _profiles.ShowActive("Gamen");

        Assert.Equal("de scène 'Gamen', die nu aan staat", _afterRestart.LightingText);
        Assert.Contains(nameof(AfterRestartViewModel.LightingText), _changed);

        _profiles.ShowActive(null);

        Assert.Equal(Strings.AfterRestartLighting, _afterRestart.LightingText);
    }

    [Fact]
    public void SleepChoice_FollowsTheBlockAbove()
    {
        Assert.Equal("wat de apparaten doen als de pc slaapt of vergrendeld is: \"Uitzetten\"", _afterRestart.SleepText);

        _effectOptions.Show(30, pauseOnLockAndSleep: true, PausedLightsMode.Dim);
        Assert.Equal("wat de apparaten doen als de pc slaapt of vergrendeld is: \"Dimmen\"", _afterRestart.SleepText);
        Assert.Contains(nameof(AfterRestartViewModel.SleepText), _changed);

        _effectOptions.PauseOnLockAndSleep = false;
        Assert.Equal(Strings.AfterRestartSleepNoPause, _afterRestart.SleepText);
    }

    [Fact]
    public void AutoStartOff_SaysTheLightingDoesNotComeBackByItself()
    {
        Assert.False(_afterRestart.StartsWithWindows);
        Assert.Contains("niet vanzelf terug", _afterRestart.AutoStartText, StringComparison.Ordinal);
        Assert.True(_afterRestart.TurnOnAutoStartCommand.CanExecute(null));
    }

    // The button does what the switch does: the switch raises Switched, which MainViewModel sends.
    [Fact]
    public void TurnOnAutoStart_SwitchesTheOptionOn()
    {
        var switched = new List<bool>();
        _autoStart.Switched += (_, on) => switched.Add(on);

        _afterRestart.TurnOnAutoStartCommand.Execute(null);

        Assert.True(_autoStart.IsOn);
        Assert.Equal([true], switched);
        Assert.True(_afterRestart.StartsWithWindows);
        Assert.Equal(Strings.AfterRestartAutoStartOn, _afterRestart.AutoStartText);
        Assert.False(_afterRestart.TurnOnAutoStartCommand.CanExecute(null));
        Assert.Contains(nameof(AfterRestartViewModel.AutoStartText), _changed);
    }

    // The engine or the tray menu changed it: the block follows, without sending anything.
    [Fact]
    public void AutoStartShown_Follows()
    {
        var switched = new List<bool>();
        _autoStart.Switched += (_, on) => switched.Add(on);

        _autoStart.Show(true);

        Assert.True(_afterRestart.StartsWithWindows);
        Assert.Empty(switched);
    }

    // "Gemiste tijden inhalen" with a scene that has a time: the engine puts that scene on at the
    // start, so the block says so; not without a time, and not with the switch off.
    [Fact]
    public void CatchUpWithASceneTime_SaysTheSceneComesOn()
    {
        _profiles.Show(["Nacht", "Gamen"]);
        _profiles.ShowCard("Gamen", Lighting, ProfileAutomation.None);
        _sceneTimes.Show(true);
        Assert.False(_afterRestart.CatchesUpSceneTimes);

        _profiles.ShowCard("Nacht", Lighting, new ProfileAutomation(new TimeOnly(23, 0)));
        Assert.True(_afterRestart.CatchesUpSceneTimes);
        Assert.Contains(nameof(AfterRestartViewModel.CatchesUpSceneTimes), _changed);

        _changed.Clear();
        _sceneTimes.IsOn = false;
        Assert.False(_afterRestart.CatchesUpSceneTimes);
        Assert.Contains(nameof(AfterRestartViewModel.CatchesUpSceneTimes), _changed);
    }

    // The scene with the time is deleted: nothing comes on by itself any more.
    [Fact]
    public void SceneWithATimeGone_SaysNothingAboutIt()
    {
        _sceneTimes.Show(true);
        _profiles.Show(["Nacht"]);
        _profiles.ShowCard("Nacht", Lighting, new ProfileAutomation(new TimeOnly(23, 0)));
        Assert.True(_afterRestart.CatchesUpSceneTimes);

        _profiles.Show([]);

        Assert.False(_afterRestart.CatchesUpSceneTimes);
    }

    // "Uit om" and "Aan om" of Hele huis are named while one is on, and with "Gemiste tijden inhalen"
    // that one that passed comes at the start.
    [Fact]
    public void WholeHouseTimes_AreNamedWhileOn()
    {
        Assert.False(_afterRestart.HasWholeHouseTimes);
        Assert.Null(_afterRestart.WholeHouseText);

        _wholeHouse.Show(new WholeHouseTimes([new LoadTime(new TimeOnly(23, 0))], [new LoadTime(new TimeOnly(7, 0), WeekDays.Workdays)]));

        Assert.True(_afterRestart.HasWholeHouseTimes);
        Assert.Equal("de tijden van Hele huis op Apparaten: uit om 23:00 (elke dag) en aan om 07:00 (werkdagen)", _afterRestart.WholeHouseText);
        Assert.Contains(nameof(AfterRestartViewModel.WholeHouseText), _changed);
        Assert.False(_afterRestart.CatchesUpWholeHouseTimes);

        _sceneTimes.IsOn = true;
        Assert.True(_afterRestart.CatchesUpWholeHouseTimes);
        Assert.Contains(nameof(AfterRestartViewModel.CatchesUpWholeHouseTimes), _changed);

        _wholeHouse.Show(WholeHouseTimes.None);
        Assert.False(_afterRestart.HasWholeHouseTimes);
        Assert.False(_afterRestart.CatchesUpWholeHouseTimes);
    }

    private static LightingInfo Lighting => new(RgbColor.White, 1.0, EffectIds.Static);
}
