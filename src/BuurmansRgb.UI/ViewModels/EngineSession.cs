using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.ViewModels;

// The connection of the settings app to the engine, and the one way to call it. MainViewModel
// attaches each new connection and detaches it when it goes; the coordinators that call the engine
// for it (LampSync, SceneCardLoader, SettingsBackupViewModel, SharingViewModel) get this session.
// Calls that change something go through the call queue (Post), in the order the user made them,
// a newer one with the same key replacing one that still waits. An answer is only shown while the
// connection that gave it is still the current one (Read, WhenCurrent, IsCurrent): after
// reconnecting, an answer of the old engine would undo what the new one said. A failed call goes
// to onCallFailed, a call that went through to onCallSucceeded, both on the UI thread. Used on the
// UI thread.
internal sealed class EngineSession
{
    private readonly CoalescingCallQueue _calls;
    private readonly Action<Exception> _onCallFailed;

    public EngineSession(UiThread ui, Action<Exception> onCallFailed, Action<object> onCallSucceeded)
    {
        Ui = ui;
        _onCallFailed = onCallFailed;
        _calls = new CoalescingCallQueue(exception => ui.Run(() => onCallFailed(exception)), key => ui.Run(() => onCallSucceeded(key)));
    }

    public UiThread Ui { get; }

    // The connection now, or null without one.
    public IEngineConnection? Current { get; private set; }

    public IEngineApi? Api => Current?.Api;

    public bool HasConnection => Current is not null;

    public void Attach(IEngineConnection connection) => Current = connection;

    // Lets go of the connection, which the caller disposes; null when there was none.
    public IEngineConnection? Detach()
    {
        var connection = Current;
        Current = null;
        return connection;
    }

    // Whether connection, such as the one an answer came from or the sender of an event, is the
    // current one.
    public bool IsCurrent(object? connection) => ReferenceEquals(connection, Current);

    // The status line gets a message for a call the engine refused; every failure goes to the log.
    public void OnCallFailed(Exception exception) => _onCallFailed(exception);

    // For a call that only reads and whose failure needs no message: only to the log.
    public static void OnReadFailed(Exception exception) => Trace.TraceError(exception.ToString());

    // In the queue on the current connection; false, and nothing sent, without one.
    public bool Post(object key, Func<IEngineApi, Task> call) => Post(key, (IEngineConnection connection) => call(connection.Api));

    // The same, for a call that checks with WhenCurrent or IsCurrent whether its connection is
    // still there.
    public bool Post(object key, Func<IEngineConnection, Task> call)
    {
        if (Current is not { } connection)
        {
            return false;
        }

        _calls.Post(key, () => call(connection));
        return true;
    }

    // In the queue, behind the changes that still wait; show gets the answer on the UI thread, unless
    // the connection was made again meanwhile.
    public bool Read<T>(object key, Func<IEngineApi, Task<T>> call, Action<T> show) =>
        Post(key, async (IEngineConnection connection) =>
        {
            var value = await call(connection.Api);
            WhenCurrent(connection, () => show(value));
        });

    // Runs action on the UI thread while connection is still the current one.
    public void WhenCurrent(IEngineConnection connection, Action action) =>
        Ui.Run(() =>
        {
            if (IsCurrent(connection))
            {
                action();
            }
        });

    // One call whose answer is awaited, outside the queue. A failure goes to OnCallFailed and gives
    // Ok false; the caller still checks with IsCurrent that the answer is not out of date.
    public async Task<(bool Ok, T Value)> TryCallAsync<T>(IEngineConnection connection, Func<IEngineApi, Task<T>> call)
    {
        try
        {
            return (true, await call(connection.Api));
        }
        catch (Exception exception)
        {
            OnCallFailed(exception);
            return (false, default!);
        }
    }
}
