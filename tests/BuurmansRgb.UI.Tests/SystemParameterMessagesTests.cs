using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// The messages of Windows about one system parameter, such as "Animatie-effecten"
// (SystemAnimationEffects), handed over by the test itself: the setting of Windows is never read
// or changed.
public sealed class SystemParameterMessagesTests
{
    private static readonly PropertyChangedEventArgs ClientAreaAnimation = new(nameof(SystemParameters.ClientAreaAnimation));

    [Theory]
    [InlineData(nameof(SystemParameters.ClientAreaAnimation), 1)]
    [InlineData(nameof(SystemParameters.HighContrast), 0)]
    [InlineData(nameof(SystemParameters.MenuAnimation), 0)]
    [InlineData(nameof(SystemParameters.WorkArea), 0)]
    public void SystemParameter_CountsOnlyForItsOwn(string parameter, int expected) => Sta.Run(() =>
    {
        var (messages, changes) = Listen();

        messages.OnSystemParameterChanged(null, new PropertyChangedEventArgs(parameter));
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(expected, changes.Count);
    });

    // A burst of messages for one change gives one Changed, on the dispatcher, a little later.
    [Fact]
    public void TwoMessagesInARow_GiveOneChangedOnTheDispatcher() => Sta.Run(() =>
    {
        var (messages, changes) = Listen();

        messages.OnSystemParameterChanged(null, ClientAreaAnimation);
        messages.OnSystemParameterChanged(null, ClientAreaAnimation);
        Assert.Empty(changes);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal([Environment.CurrentManagedThreadId], changes);
    });

    [Fact]
    public void AMessageAfterAChanged_GivesAnotherChanged() => Sta.Run(() =>
    {
        var (messages, changes) = Listen();

        messages.OnSystemParameterChanged(null, ClientAreaAnimation);
        OffscreenWindow.WaitUntilIdle();
        messages.OnSystemParameterChanged(null, ClientAreaAnimation);
        OffscreenWindow.WaitUntilIdle();

        Assert.Equal(2, changes.Count);
    });

    // The messages for "Animatie-effecten" on the dispatcher of this thread, and per Changed the
    // thread it came on.
    private static (SystemParameterMessages Messages, List<int> Changes) Listen()
    {
        var messages = new SystemParameterMessages(Dispatcher.CurrentDispatcher, nameof(SystemParameters.ClientAreaAnimation));
        var changes = new List<int>();
        messages.Changed += (_, _) => changes.Add(Environment.CurrentManagedThreadId);
        return (messages, changes);
    }
}
