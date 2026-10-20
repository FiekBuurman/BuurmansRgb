using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace BuurmansRgb.UI.Views;

// Runs Started when the user takes the thumb of a slider and Completed when they let it go, so a
// view model can wait for the end of a drag instead of acting on every value on the way. Arrow keys
// and clicks beside the thumb move no thumb and run neither.
public static class SliderDrag
{
    public static readonly DependencyProperty StartedProperty = DependencyProperty.RegisterAttached(
        "Started", typeof(ICommand), typeof(SliderDrag), new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CompletedProperty = DependencyProperty.RegisterAttached(
        "Completed", typeof(ICommand), typeof(SliderDrag), new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetStarted(DependencyObject element) => (ICommand?)element.GetValue(StartedProperty);

    public static void SetStarted(DependencyObject element, ICommand? value) => element.SetValue(StartedProperty, value);

    public static ICommand? GetCompleted(DependencyObject element) => (ICommand?)element.GetValue(CompletedProperty);

    public static void SetCompleted(DependencyObject element, ICommand? value) => element.SetValue(CompletedProperty, value);

    // The drag events of the thumb bubble up to the element the commands are set on.
    private static void OnCommandChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not UIElement uiElement)
        {
            return;
        }

        uiElement.RemoveHandler(Thumb.DragStartedEvent, (DragStartedEventHandler)OnDragStarted);
        uiElement.RemoveHandler(Thumb.DragCompletedEvent, (DragCompletedEventHandler)OnDragCompleted);
        if (GetStarted(element) is not null || GetCompleted(element) is not null)
        {
            uiElement.AddHandler(Thumb.DragStartedEvent, (DragStartedEventHandler)OnDragStarted);
            uiElement.AddHandler(Thumb.DragCompletedEvent, (DragCompletedEventHandler)OnDragCompleted);
        }
    }

    private static void OnDragStarted(object sender, DragStartedEventArgs e) => Run(GetStarted((DependencyObject)sender));

    private static void OnDragCompleted(object sender, DragCompletedEventArgs e) => Run(GetCompleted((DependencyObject)sender));

    private static void Run(ICommand? command)
    {
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}
