namespace BuurmansRgb.UI.ViewModels;

// The UI thread of whoever made it: the SynchronizationContext that was current then. Timers and
// calls to the engine finish on the thread pool; what they change in a viewmodel belongs to the UI
// thread. Without a context (tests that make a viewmodel outside WPF) both run the action at once.
internal sealed class UiThread
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    // Whether an action would run at once here: this is the UI thread, or there is none.
    public bool IsCurrent => _context is null || SynchronizationContext.Current == _context;

    // Runs the action now when this already is the UI thread, else after the current UI work.
    public void Run(Action action)
    {
        if (IsCurrent)
        {
            action();
        }
        else
        {
            Post(action);
        }
    }

    // Runs the action after the current UI work, also when this is the UI thread.
    public void Post(Action action)
    {
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), state: null);
        }
    }
}
