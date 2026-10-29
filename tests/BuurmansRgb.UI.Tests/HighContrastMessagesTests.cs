using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using BuurmansRgb.UI.Services;
using Microsoft.Win32;

namespace BuurmansRgb.UI.Tests;

// The messages of Windows about its contrast theme and colors, handed over by the test itself: the
// setting of Windows is never read or changed, and no handler goes on SystemEvents.
public sealed class HighContrastMessagesTests
{
    private static readonly PropertyChangedEventArgs HighContrast = new(nameof(SystemParameters.HighContrast));
    private static readonly UserPreferenceChangedEventArgs Colors = new(UserPreferenceCategory.Color);

    // Windows sends a burst of messages for one change: ContrastTheme reads the colors once.
    [Fact]
    public void TwoMessagesInARow_GiveOneChangedOnTheDispatcher() => Sta.Run(() =>
    {
        var (messages, changes) = Listen();

        messages.OnSystemParameterChanged(null, HighContrast);
        messages.OnUserPreferenceChanged(this, Colors);
        Assert.Empty(changes);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal([Environment.CurrentManagedThreadId], changes);
    });

    // SystemEvents may tell from a thread of its own; Changed still comes on the dispatcher.
    [Fact]
    public void MessagesFromAnotherThread_GiveOneChangedOnTheDispatcher() => Sta.Run(() =>
    {
        var (messages, changes) = Listen();

        var other = new Thread(() =>
        {
            messages.OnUserPreferenceChanged(this, Colors);
            messages.OnUserPreferenceChanged(this, Colors);
        });
        other.Start();
        other.Join();
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal([Environment.CurrentManagedThreadId], changes);
    });

    // After a Changed, the next message gives a Changed of its own.
    [Fact]
    public void AMessageAfterAChanged_GivesAnotherChanged() => Sta.Run(() =>
    {
        var (messages, changes) = Listen();

        messages.OnSystemParameterChanged(null, HighContrast);
        OffscreenWindow.WaitUntilIdle();
        messages.OnSystemParameterChanged(null, HighContrast);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(2, changes.Count);
    });

    [Theory]
    [InlineData(nameof(SystemParameters.HighContrast), 1)]
    [InlineData(nameof(SystemParameters.KeyboardCues), 0)]
    [InlineData(nameof(SystemParameters.WorkArea), 0)]
    public void SystemParameter_CountsOnlyForHighContrast(string parameter, int expected) => Sta.Run(() =>
    {
        var (messages, changes) = Listen();

        messages.OnSystemParameterChanged(null, new PropertyChangedEventArgs(parameter));
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(expected, changes.Count);
    });

    [Theory]
    [InlineData(UserPreferenceCategory.Color, 1)]
    [InlineData(UserPreferenceCategory.General, 0)]
    [InlineData(UserPreferenceCategory.Desktop, 0)]
    [InlineData(UserPreferenceCategory.Accessibility, 0)]
    [InlineData(UserPreferenceCategory.VisualStyle, 0)]
    public void UserPreference_CountsOnlyForColors(UserPreferenceCategory category, int expected) => Sta.Run(() =>
    {
        var (messages, changes) = Listen();

        messages.OnUserPreferenceChanged(this, new UserPreferenceChangedEventArgs(category));
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(expected, changes.Count);
    });

    // The messages for the dispatcher of this thread, and per Changed the thread it came on.
    private static (HighContrastMessages Messages, List<int> Changes) Listen()
    {
        var messages = new HighContrastMessages(Dispatcher.CurrentDispatcher);
        var changes = new List<int>();
        messages.Changed += (_, _) => changes.Add(Environment.CurrentManagedThreadId);
        return (messages, changes);
    }
}
