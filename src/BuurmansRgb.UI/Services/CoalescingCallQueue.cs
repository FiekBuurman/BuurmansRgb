namespace BuurmansRgb.UI.Services;

// Runs engine calls one at a time, in order. A call replaces the queued call with the same key
// when that one is last in line, so a dragged slider only sends its latest value. onSucceeded
// hears about every call that went through, with its key, so a message about an earlier one can
// go and a preview that stands still can show what the call changed.
public sealed class CoalescingCallQueue(Action<Exception> onError, Action<object>? onSucceeded = null)
{
    private readonly Lock _gate = new();
    private readonly List<(object Key, Func<Task> Call)> _queue = [];
    private bool _running;

    public void Post(object key, Func<Task> call)
    {
        lock (_gate)
        {
            if (_queue.Count > 0 && Equals(_queue[^1].Key, key))
            {
                _queue[^1] = (key, call);
            }
            else
            {
                _queue.Add((key, call));
            }

            if (_running)
            {
                return;
            }

            _running = true;
        }

        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        while (true)
        {
            object key;
            Func<Task> call;
            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    _running = false;
                    return;
                }

                (key, call) = _queue[0];
                _queue.RemoveAt(0);
            }

            try
            {
                await call();
            }
            catch (Exception exception)
            {
                onError(exception);
                continue;
            }

            onSucceeded?.Invoke(key);
        }
    }
}
