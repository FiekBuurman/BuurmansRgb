namespace BuurmansRgb.Contracts.Testing;

// Every call that changes something goes through Record, which lets a test hold it back, let it
// fail, and see it in Calls.
public sealed partial class FakeEngineApi
{
    // The calls that counted, in order, such as "color fan #FF8800" or "profile save Gamen".
    public List<string> Calls { get; } = [];

    // When set, every call that changes something fails with this exception.
    public Exception? SetException { get; set; }

    // When set, a call that changes something counts once this task completes, like a busy engine,
    // so the calls after it wait in the queue of the app.
    public Task? SetGate { get; set; }

    // When set, the call with this text fails once with this exception; the calls around it go.
    public (string Call, Exception Exception)? FailOnce { get; set; }

    private Task Record(string call) => SetGate is { } gate ? RecordAfterAsync(gate, call) : RecordNow(call);

    private async Task RecordAfterAsync(Task gate, string call)
    {
        await gate;
        await RecordNow(call);
    }

    private Task RecordNow(string call)
    {
        if (SetException is not null)
        {
            return Task.FromException(SetException);
        }

        if (FailOnce is { } fail && fail.Call == call)
        {
            FailOnce = null;
            return Task.FromException(fail.Exception);
        }

        Calls.Add(call);
        return Task.CompletedTask;
    }
}
