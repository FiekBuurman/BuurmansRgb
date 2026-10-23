using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// The texts are spelled out, so a change to them is a choice.
public sealed class EffectsPauseViewModelTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly EffectsPauseViewModel _pause;

    public EffectsPauseViewModelTests() => _pause = new EffectsPauseViewModel(_time);

    public void Dispose() => _pause.Dispose();

    [Fact]
    public void New_IsNotPaused()
    {
        Assert.False(_pause.IsPaused);
        Assert.False(_pause.IsShown);
        Assert.False(_pause.ResumeCommand.CanExecute(null));
        Assert.Equal(string.Empty, _pause.Text);
    }

    [Theory]
    [InlineData(PauseReasons.Manual, "Effecten gepauzeerd via het tray-menu – het voorbeeld toont wat terugkomt na hervatten.")]
    [InlineData(PauseReasons.Locked, "Effecten gepauzeerd zolang Windows vergrendeld is – ze lopen weer na het ontgrendelen.")]
    [InlineData(PauseReasons.Asleep, "Effecten gepauzeerd vanwege de slaapstand – ze lopen weer na het ontwaken.")]
    [InlineData(PauseReasons.DisplayOff, "Effecten gepauzeerd zolang het scherm uit is – ze lopen weer als het aangaat.")]
    [InlineData(PauseReasons.ScreenSaver, "Effecten gepauzeerd zolang de schermbeveiliging loopt – ze lopen weer als je de muis beweegt of een toets indrukt.")]
    public void Show_TellsTheReason(PauseReasons reasons, string text)
    {
        _pause.Show(reasons);

        Assert.True(_pause.IsPaused);
        Assert.Equal(text, _pause.Text);
    }

    // The pause the user can end comes first; waking up goes to the lock screen, and the screen
    // goes on before the pc is unlocked or awake; behind a screen that is off the screen saver may
    // still run.
    [Theory]
    [InlineData(PauseReasons.Manual | PauseReasons.Locked | PauseReasons.Asleep, PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked | PauseReasons.Asleep, PauseReasons.Locked)]
    [InlineData(PauseReasons.Manual | PauseReasons.DisplayOff, PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked | PauseReasons.DisplayOff, PauseReasons.Locked)]
    [InlineData(PauseReasons.Asleep | PauseReasons.DisplayOff, PauseReasons.Asleep)]
    [InlineData(PauseReasons.Manual | PauseReasons.ScreenSaver, PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked | PauseReasons.ScreenSaver, PauseReasons.Locked)]
    [InlineData(PauseReasons.Asleep | PauseReasons.ScreenSaver, PauseReasons.Asleep)]
    [InlineData(PauseReasons.ScreenSaver | PauseReasons.DisplayOff, PauseReasons.ScreenSaver)]
    public void Show_MoreReasons_TellsTheOneThatMattersMost(PauseReasons reasons, PauseReasons shown)
    {
        using var single = new EffectsPauseViewModel();
        single.Show(shown);

        _pause.Show(reasons);

        Assert.Equal(single.Text, _pause.Text);
    }

    [Theory]
    [InlineData(PauseReasons.Manual, true)]
    [InlineData(PauseReasons.Manual | PauseReasons.Locked, true)]
    [InlineData(PauseReasons.Locked, false)]
    [InlineData(PauseReasons.Asleep, false)]
    [InlineData(PauseReasons.DisplayOff, false)]
    [InlineData(PauseReasons.ScreenSaver, false)]
    public void Resume_OnlyForThePauseFromTheTrayMenu(PauseReasons reasons, bool canResume)
    {
        _pause.Show(reasons);

        Assert.Equal(canResume, _pause.CanResume);
        Assert.Equal(canResume, _pause.ResumeCommand.CanExecute(null));
    }

    [Fact]
    public void Resume_AsksAndChangesNothingItself()
    {
        var requests = 0;
        _pause.ResumeRequested += (_, _) => requests++;
        _pause.Show(PauseReasons.Manual);

        _pause.ResumeCommand.Execute(null);

        Assert.Equal(1, requests);
        Assert.Equal(PauseReasons.Manual, _pause.Reasons);
    }

    [Fact]
    public void Show_TellsTheViewWhatChanged()
    {
        var changed = new List<string?>();
        _pause.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _pause.Show(PauseReasons.Locked);

        Assert.Contains(nameof(EffectsPauseViewModel.IsPaused), changed);
        Assert.Contains(nameof(EffectsPauseViewModel.Text), changed);
        Assert.Contains(nameof(EffectsPauseViewModel.CanResume), changed);
    }

    // A screen reader reads only what shows, so the end of the pause stays for a moment.
    [Fact]
    public void PauseEnds_SaysSoForAMomentThenHides()
    {
        _pause.Show(PauseReasons.Manual);

        _pause.Show(PauseReasons.None);
        Assert.False(_pause.IsPaused);
        Assert.False(_pause.CanResume);
        Assert.True(_pause.IsShown);
        Assert.Equal("Effecten lopen weer.", _pause.Text);

        _time.Advance(EffectsPauseViewModel.ResumedMessageTime - TimeSpan.FromMilliseconds(1));
        Assert.True(_pause.IsShown);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(_pause.IsShown);
        Assert.Equal(string.Empty, _pause.Text);
    }

    [Fact]
    public void PauseAgainWhileSayingItEnded_ShowsThePauseAndKeepsIt()
    {
        _pause.Show(PauseReasons.Manual);
        _pause.Show(PauseReasons.None);

        _pause.Show(PauseReasons.Locked);
        _time.Advance(EffectsPauseViewModel.ResumedMessageTime);

        Assert.True(_pause.IsShown);
        Assert.False(_pause.HasResumed);
        Assert.Equal("Effecten gepauzeerd zolang Windows vergrendeld is – ze lopen weer na het ontgrendelen.", _pause.Text);
    }

    // The text changes from the pause to its end in one step, so the message never hides in between.
    [Fact]
    public void PauseEnds_NeverHidesTheMessageOnTheWay()
    {
        _pause.Show(PauseReasons.Manual);
        var hidden = false;
        _pause.PropertyChanged += (_, _) => hidden |= !_pause.IsShown;

        _pause.Show(PauseReasons.None);

        Assert.False(hidden);
    }

    [Fact]
    public void Clear_AfterAPauseEnded_SaysNothing()
    {
        _pause.Show(PauseReasons.Manual);
        _pause.Show(PauseReasons.None);

        _pause.Clear();

        Assert.False(_pause.IsShown);
    }

    [Fact]
    public void Clear_ShowsNoPause()
    {
        _pause.Show(PauseReasons.Manual);

        _pause.Clear();

        Assert.False(_pause.IsPaused);
        Assert.False(_pause.IsShown);
    }
}
