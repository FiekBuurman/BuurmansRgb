using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The preview stands still while "Animatie-effecten" of Windows is off, with a fake setting: the
// setting of Windows is never read or changed.
public sealed class PreviewMotionViewModelTests
{
    private readonly FakeAnimationEffects _animations = new();

    [Fact]
    public void AnimationsOn_MovesWithoutBar()
    {
        using var motion = new PreviewMotionViewModel(_animations);

        Assert.False(motion.IsStill);
        Assert.False(motion.IsShown);
        Assert.False(motion.ToggleCommand.CanExecute(null));
    }

    [Fact]
    public void AnimationsOff_StandsStillAndSaysWhy()
    {
        _animations.IsOn = false;
        using var motion = new PreviewMotionViewModel(_animations);

        Assert.True(motion.IsStill);
        Assert.True(motion.IsShown);
        Assert.Equal("Voorbeeld staat stil omdat animaties in Windows uit staan.", motion.Text);
        Assert.Equal("Laten lopen", motion.ButtonText);
        Assert.Equal(Strings.PreviewRunAccessibleName, motion.ButtonAccessibleName);
        Assert.Equal(Strings.PreviewRunHelp, motion.ButtonHelpText);
    }

    // "Laten lopen" lets it move, "Stilzetten" stills it again; each says StillChanged once.
    [Fact]
    public void Toggle_RunsAndStillsAgain()
    {
        _animations.IsOn = false;
        using var motion = new PreviewMotionViewModel(_animations);
        var changes = 0;
        motion.StillChanged += (_, _) => changes++;

        motion.ToggleCommand.Execute(null);
        Assert.False(motion.IsStill);
        Assert.True(motion.IsShown);
        Assert.Equal("Stilzetten", motion.ButtonText);
        Assert.Equal(Strings.PreviewRunsAnyway, motion.Text);
        Assert.Equal(Strings.PreviewStopAccessibleName, motion.ButtonAccessibleName);

        motion.ToggleCommand.Execute(null);
        Assert.True(motion.IsStill);
        Assert.Equal(2, changes);
    }

    // Windows turns animations off and on while the app is open.
    [Fact]
    public void AnimationsTurnedOffAndOn_FollowsAtOnce()
    {
        using var motion = new PreviewMotionViewModel(_animations);
        var changed = new List<string?>();
        motion.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var stills = new List<bool>();
        motion.StillChanged += (_, _) => stills.Add(motion.IsStill);

        _animations.Turn(on: false);
        Assert.True(motion.IsStill);
        Assert.Contains(nameof(PreviewMotionViewModel.IsShown), changed);
        Assert.Contains(nameof(PreviewMotionViewModel.Text), changed);

        _animations.Turn(on: true);
        Assert.False(motion.IsStill);
        Assert.False(motion.IsShown);
        Assert.Equal([true, false], stills);
    }

    // The choice to let it run holds for the session, also when Windows turns animations on and
    // off again meanwhile.
    [Fact]
    public void LetRun_HoldsWhenAnimationsGoOnAndOffAgain()
    {
        _animations.IsOn = false;
        using var motion = new PreviewMotionViewModel(_animations);
        motion.ToggleCommand.Execute(null);

        _animations.Turn(on: true);
        _animations.Turn(on: false);

        Assert.False(motion.IsStill);
        Assert.True(motion.RunsAnyway);
    }

    // Another setting of Windows that changes nothing here says nothing.
    [Fact]
    public void ChangedWithTheSameSetting_SaysNoStillChanged()
    {
        using var motion = new PreviewMotionViewModel(_animations);
        var changes = 0;
        motion.StillChanged += (_, _) => changes++;

        _animations.Turn(on: true);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void Dispose_StopsFollowing()
    {
        var motion = new PreviewMotionViewModel(_animations);
        motion.Dispose();

        _animations.Turn(on: false);

        Assert.False(_animations.HasListeners);
    }
}

// "Animatie-effecten" that a test turns on and off, as Windows would.
internal sealed class FakeAnimationEffects : IAnimationEffects
{
    public event EventHandler? Changed;

    public bool IsOn { get; set; } = true;

    public bool HasListeners => Changed is not null;

    public void Turn(bool on)
    {
        IsOn = on;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
