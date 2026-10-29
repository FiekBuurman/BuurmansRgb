using System.ComponentModel;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Services;

// The messages of Windows about one of the SystemParameters of WPF, as one Changed on the
// dispatcher: a change of that parameter counts, other parameters do not. Changed comes a little
// later, once the dispatcher has nothing more urgent to do, so WPF has read the new value by then,
// and it comes once for the burst of messages Windows sends for one change. A message may come from
// a thread of its own, so the flag for that burst is set atomically. The class of the system
// setting hands it the messages (SystemParameters.StaticPropertyChanged); a test hands it its own.
internal class SystemParameterMessages(Dispatcher dispatcher, string parameter)
{
    private int _pending;

    public event EventHandler? Changed;

    public void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == parameter)
        {
            Post();
        }
    }

    // Also for another message that counts, such as a change of the colors of Windows.
    protected void Post()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            Volatile.Write(ref _pending, 0);
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }
}
