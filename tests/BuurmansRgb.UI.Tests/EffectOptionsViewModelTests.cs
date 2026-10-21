using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class EffectOptionsViewModelTests
{
    private readonly EffectOptionsViewModel _options = new();

    [Fact]
    public void Show_AStandardRate_SelectsItAmongTheThree()
    {
        _options.Show(30, pauseOnLockAndSleep: true);

        Assert.Equal([15, 30, 60], _options.FrameRates.Select(rate => rate.FramesPerSecond));
        Assert.Equal(Strings.FrameRateNormal, _options.SelectedFrameRate?.DisplayName);
        Assert.True(_options.PauseOnLockAndSleep);
    }

    // A rate written in settings.json by hand stays as it is until the user picks another.
    [Fact]
    public void Show_AnotherRate_AddsItInOrder()
    {
        _options.Show(24, pauseOnLockAndSleep: false);

        Assert.Equal([15, 24, 30, 60], _options.FrameRates.Select(rate => rate.FramesPerSecond));
        Assert.Equal("24 beelden per seconde", _options.SelectedFrameRate?.DisplayName);
        Assert.False(_options.PauseOnLockAndSleep);

        _options.Show(60, pauseOnLockAndSleep: false);
        Assert.Equal([15, 30, 60], _options.FrameRates.Select(rate => rate.FramesPerSecond));
    }

    [Fact]
    public void Show_RaisesNothing()
    {
        var raised = 0;
        _options.FramesPerSecondChanged += (_, _) => raised++;
        _options.PauseOnLockAndSleepChanged += (_, _) => raised++;

        _options.Show(15, pauseOnLockAndSleep: false);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Picking_RaisesTheNewValue()
    {
        _options.Show(30, pauseOnLockAndSleep: true);
        var framesPerSecond = new List<int>();
        var pauses = new List<bool>();
        _options.FramesPerSecondChanged += (_, value) => framesPerSecond.Add(value);
        _options.PauseOnLockAndSleepChanged += (_, value) => pauses.Add(value);

        _options.SelectedFrameRate = _options.FrameRates[^1];
        _options.PauseOnLockAndSleep = false;

        Assert.Equal([60], framesPerSecond);
        Assert.Equal([false], pauses);
    }
}
