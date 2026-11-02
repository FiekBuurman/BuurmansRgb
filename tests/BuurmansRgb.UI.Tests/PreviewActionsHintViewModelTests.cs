using BuurmansRgb.Contracts;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The line above the preview that says why Knipperen and Tellen are grey. The texts are spelled
// out, so a change to them is a choice.
public sealed class PreviewActionsHintViewModelTests
{
    private readonly PreviewActionsHintViewModel _hint = new() { HasButtons = true };

    [Fact]
    public void New_ShowsNothing()
    {
        Assert.False(_hint.IsShown);
        Assert.False(_hint.CanResume);
        Assert.False(_hint.ResumeCommand.CanExecute(null));
        Assert.Equal(string.Empty, _hint.Text);
    }

    // Only the pause from the tray menu can end here, so only that one has "Hervatten".
    [Theory]
    [InlineData(PauseReasons.Manual, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn.", true)]
    [InlineData(PauseReasons.Locked, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn omdat Windows vergrendeld is.", false)]
    [InlineData(PauseReasons.Asleep, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn omdat de pc in de slaapstand is.", false)]
    [InlineData(PauseReasons.ScreenSaver, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn omdat de schermbeveiliging loopt.", false)]
    [InlineData(PauseReasons.DisplayOff, "Knipperen en Tellen kunnen niet zolang de effecten gepauzeerd zijn omdat het scherm uit is.", false)]
    public void Reasons_TellTheReason(PauseReasons reasons, string text, bool canResume)
    {
        _hint.Reasons = reasons;

        Assert.True(_hint.IsShown);
        Assert.Equal(text, _hint.Text);
        Assert.Equal(canResume, _hint.CanResume);
        Assert.Equal(canResume, _hint.ResumeCommand.CanExecute(null));
    }

    // The same order as the pause message at the top of the page (EffectsPauseViewModel).
    [Theory]
    [InlineData(PauseReasons.Manual | PauseReasons.Locked | PauseReasons.Asleep, PauseReasons.Manual)]
    [InlineData(PauseReasons.Locked | PauseReasons.Asleep | PauseReasons.DisplayOff, PauseReasons.Locked)]
    [InlineData(PauseReasons.Asleep | PauseReasons.ScreenSaver, PauseReasons.Asleep)]
    [InlineData(PauseReasons.ScreenSaver | PauseReasons.DisplayOff, PauseReasons.ScreenSaver)]
    public void MoreReasons_TellTheOneThatMattersMost(PauseReasons reasons, PauseReasons shown)
    {
        var single = new PreviewActionsHintViewModel { Reasons = shown };

        _hint.Reasons = reasons;

        Assert.Equal(single.Text, _hint.Text);
    }

    // Without a device in the preview there is no button to explain.
    [Fact]
    public void WithoutButtons_ShowsNothing()
    {
        _hint.Reasons = PauseReasons.Manual;

        _hint.HasButtons = false;

        Assert.False(_hint.IsShown);
        Assert.Equal(PauseReasons.Manual, _hint.Reasons);
    }

    [Fact]
    public void PauseEnds_LineGoes()
    {
        _hint.Reasons = PauseReasons.Locked;

        _hint.Reasons = PauseReasons.None;

        Assert.False(_hint.IsShown);
        Assert.Equal(string.Empty, _hint.Text);
    }

    [Fact]
    public void Resume_AsksToEndThePause()
    {
        _hint.Reasons = PauseReasons.Manual;
        var asked = 0;
        _hint.ResumeRequested += (_, _) => asked++;

        _hint.ResumeCommand.Execute(null);

        Assert.Equal(1, asked);
    }
}
