using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The texts are spelled out, so a change to them is a choice.
public sealed class EffectsPauseViewModelTests
{
    private readonly EffectsPauseViewModel _pause = new();

    [Fact]
    public void New_IsNotPaused()
    {
        Assert.False(_pause.IsPaused);
        Assert.False(_pause.ResumeCommand.CanExecute(null));
        Assert.Equal(string.Empty, _pause.Text);
    }

    [Theory]
    [InlineData(PauseReasons.Manual, "Effecten gepauzeerd via het tray-menu – het voorbeeld toont wat terugkomt na hervatten.")]
    [InlineData(PauseReasons.Locked, "Effecten gepauzeerd zolang Windows vergrendeld is – ze lopen weer na het ontgrendelen.")]
    [InlineData(PauseReasons.Asleep, "Effecten gepauzeerd voor de slaapstand – ze lopen weer na het ontwaken.")]
    public void Show_TellsTheReason(PauseReasons reasons, string text)
    {
        _pause.Show(reasons);

        Assert.True(_pause.IsPaused);
        Assert.Equal(text, _pause.Text);
    }

    // The pause the user can end comes first; waking up goes to the lock screen.
    [Theory]
    [InlineData(PauseReasons.Manual | PauseReasons.Locked | PauseReasons.Asleep, PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked | PauseReasons.Asleep, PauseReasons.Locked)]
    public void Show_MoreReasons_TellsTheOneThatMattersMost(PauseReasons reasons, PauseReasons shown)
    {
        var single = new EffectsPauseViewModel();
        single.Show(shown);

        _pause.Show(reasons);

        Assert.Equal(single.Text, _pause.Text);
    }

    [Theory]
    [InlineData(PauseReasons.Manual, true)]
    [InlineData(PauseReasons.Manual | PauseReasons.Locked, true)]
    [InlineData(PauseReasons.Locked, false)]
    [InlineData(PauseReasons.Asleep, false)]
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

    [Fact]
    public void Clear_ShowsNoPause()
    {
        _pause.Show(PauseReasons.Manual);

        _pause.Clear();

        Assert.False(_pause.IsPaused);
    }
}
