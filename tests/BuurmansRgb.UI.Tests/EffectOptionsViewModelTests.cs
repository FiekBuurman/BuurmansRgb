using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class EffectOptionsViewModelTests
{
    private readonly EffectOptionsViewModel _options = new();

    [Fact]
    public void Show_AStandardRate_SelectsItAmongTheThree()
    {
        _options.Show(30, pauseOnLockAndSleep: true, PausedLightsMode.Keep);

        Assert.Equal([15, 30, 60], _options.FrameRates.Select(rate => rate.FramesPerSecond));
        Assert.Equal(Strings.FrameRateNormal, _options.SelectedFrameRate?.DisplayName);
        Assert.True(_options.PauseOnLockAndSleep);
    }

    // A rate written in settings.json by hand stays as it is until the user picks another.
    [Fact]
    public void Show_AnotherRate_AddsItInOrder()
    {
        _options.Show(24, pauseOnLockAndSleep: false, PausedLightsMode.Keep);

        Assert.Equal([15, 24, 30, 60], _options.FrameRates.Select(rate => rate.FramesPerSecond));
        Assert.Equal("24 beelden per seconde", _options.SelectedFrameRate?.DisplayName);
        Assert.False(_options.PauseOnLockAndSleep);

        _options.Show(60, pauseOnLockAndSleep: false, PausedLightsMode.Keep);
        Assert.Equal([15, 30, 60], _options.FrameRates.Select(rate => rate.FramesPerSecond));
    }

    // From at once to two seconds, with half a second, the default, chosen before the engine tells.
    [Fact]
    public void SceneFades_AreTheFiveStandardFadesInPlainDutch_WithNormalChosen()
    {
        Assert.Equal(
            ["Direct (geen overgang)", "Kort (0,25 seconde)", "Normaal (0,5 seconde)", "Rustig (1 seconde)", "Langzaam (2 seconden)"],
            _options.SceneFades.Select(option => option.DisplayName));
        Assert.Equal([0.0, 0.25, 0.5, 1.0, 2.0], _options.SceneFades.Select(option => option.Fade.TotalSeconds));
        Assert.Equal(SceneFade.Default, _options.SelectedSceneFade?.Fade);
        Assert.Equal("Normaal (0,5 seconde)", _options.SelectedSceneFade?.ToString());
    }

    // A fade written in settings.json by hand stays as it is until the user picks another.
    [Fact]
    public void ShowSceneFade_AnotherFade_AddsItInOrder_WithoutRaising()
    {
        var raised = 0;
        _options.SceneFadeChanged += (_, _) => raised++;

        _options.ShowSceneFade(TimeSpan.FromSeconds(1.5));

        Assert.Equal([0.0, 0.25, 0.5, 1.0, 1.5, 2.0], _options.SceneFades.Select(option => option.Fade.TotalSeconds));
        Assert.Equal("1,5 seconde", _options.SelectedSceneFade?.DisplayName);

        _options.ShowSceneFade(TimeSpan.Zero);
        Assert.Equal(5, _options.SceneFades.Count);
        Assert.Equal(TimeSpan.Zero, _options.SelectedSceneFade?.Fade);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void PickingASceneFade_RaisesIt()
    {
        var fades = new List<TimeSpan>();
        _options.SceneFadeChanged += (_, fade) => fades.Add(fade);

        _options.SelectedSceneFade = _options.SceneFades[^1];

        Assert.Equal([SceneFade.Max], fades);
    }

    [Fact]
    public void PausedLightsOptions_AreTheThreeModesInPlainDutch_WithOffFirst()
    {
        Assert.Equal(
            ["Uitzetten", "Dimmen", "Laten branden"],
            EffectOptionsViewModel.PausedLightsOptions.Select(option => option.DisplayName));
        Assert.Equal(
            [PausedLightsMode.Off, PausedLightsMode.Dim, PausedLightsMode.Keep],
            EffectOptionsViewModel.PausedLightsOptions.Select(option => option.Mode));
        Assert.Equal(PausedLightsMode.Off, _options.SelectedPausedLights?.Mode);
    }

    [Theory]
    [InlineData(PausedLightsMode.Keep)]
    [InlineData(PausedLightsMode.Dim)]
    [InlineData(PausedLightsMode.Off)]
    public void Show_SelectsWhatThePausedLightsShow(PausedLightsMode mode)
    {
        _options.Show(30, pauseOnLockAndSleep: true, mode);

        Assert.Equal(mode, _options.SelectedPausedLights?.Mode);
    }

    // A screen reader hears why the choice does nothing while the pause is off, before the explanation.
    [Fact]
    public void PausedLightsHelpText_WithoutThePause_StartsWithWhy()
    {
        var changed = new List<string?>();
        _options.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal(Strings.PausedLightsHint, _options.PausedLightsHelpText);

        _options.PauseOnLockAndSleep = false;

        Assert.Equal($"{Strings.PausedLightsNeedsPause} {Strings.PausedLightsHint}", _options.PausedLightsHelpText);
        Assert.Contains(nameof(EffectOptionsViewModel.PausedLightsHelpText), changed);
    }

    [Fact]
    public void Show_RaisesNothing()
    {
        var raised = 0;
        _options.FramesPerSecondChanged += (_, _) => raised++;
        _options.PauseOnLockAndSleepChanged += (_, _) => raised++;
        _options.PausedLightsChanged += (_, _) => raised++;

        _options.Show(15, pauseOnLockAndSleep: false, PausedLightsMode.Off);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Picking_RaisesTheNewValue()
    {
        _options.Show(30, pauseOnLockAndSleep: true, PausedLightsMode.Keep);
        var framesPerSecond = new List<int>();
        var pauses = new List<bool>();
        var pausedLights = new List<PausedLightsMode>();
        _options.FramesPerSecondChanged += (_, value) => framesPerSecond.Add(value);
        _options.PauseOnLockAndSleepChanged += (_, value) => pauses.Add(value);
        _options.PausedLightsChanged += (_, value) => pausedLights.Add(value);

        _options.SelectedFrameRate = _options.FrameRates[^1];
        _options.PauseOnLockAndSleep = false;
        _options.SelectedPausedLights = EffectOptionsViewModel.PausedLightsOptions[0];

        Assert.Equal([60], framesPerSecond);
        Assert.Equal([false], pauses);
        Assert.Equal([PausedLightsMode.Off], pausedLights);
    }
}
