using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BuurmansRgb.UI.Views;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.Tests;

// With a real window off screen, so the slider has its template and thumb.
public sealed class SliderDragTests
{
    [Fact]
    public void Thumb_TakenAndLetGo_RunsStartedAndThenCompleted() => OffscreenWindow.Run(panel =>
    {
        List<string> calls = [];
        var slider = new Slider { Maximum = 60 };
        SliderDrag.SetStarted(slider, new RelayCommand(() => calls.Add("started")));
        SliderDrag.SetCompleted(slider, new RelayCommand(() => calls.Add("completed")));
        panel.Children.Add(slider);
        OffscreenWindow.WaitUntilIdle();
        var thumb = ThumbOf(slider);

        thumb.RaiseEvent(new DragStartedEventArgs(0, 0));
        thumb.RaiseEvent(new DragCompletedEventArgs(10, 0, canceled: false));

        Assert.Equal(["started", "completed"], calls);
    });

    [Fact]
    public void CommandThatCannotRun_IsNotRun() => OffscreenWindow.Run(panel =>
    {
        var runs = 0;
        var slider = new Slider();
        SliderDrag.SetCompleted(slider, new RelayCommand(() => runs++, () => false));
        panel.Children.Add(slider);
        OffscreenWindow.WaitUntilIdle();

        ThumbOf(slider).RaiseEvent(new DragCompletedEventArgs(0, 0, canceled: false));

        Assert.Equal(0, runs);
    });

    [Fact]
    public void CommandsCleared_RunNothing() => OffscreenWindow.Run(panel =>
    {
        var runs = 0;
        var slider = new Slider();
        SliderDrag.SetStarted(slider, new RelayCommand(() => runs++));
        panel.Children.Add(slider);
        OffscreenWindow.WaitUntilIdle();

        SliderDrag.SetStarted(slider, null);
        ThumbOf(slider).RaiseEvent(new DragStartedEventArgs(0, 0));

        Assert.Equal(0, runs);
    });

    private static Thumb ThumbOf(Slider slider) => ((Track)slider.Template.FindName("PART_Track", slider)).Thumb;
}
