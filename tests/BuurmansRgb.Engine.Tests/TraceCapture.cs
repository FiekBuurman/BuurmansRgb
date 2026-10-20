using System.Diagnostics;

namespace BuurmansRgb.Engine.Tests;

// The trace messages written while it lives. Trace is shared by the tests that run at the same
// time, so look for a message of your own instead of counting them all.
internal sealed class TraceCapture : TraceListener
{
    private readonly Lock _gate = new();
    private readonly List<string> _messages = [];

    public TraceCapture() => Trace.Listeners.Add(this);

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_gate)
            {
                return [.. _messages];
            }
        }
    }

    public override void Write(string? message)
    {
    }

    public override void WriteLine(string? message)
    {
        lock (_gate)
        {
            _messages.Add(message ?? string.Empty);
        }
    }

    protected override void Dispose(bool disposing)
    {
        Trace.Listeners.Remove(this);
        base.Dispose(disposing);
    }
}
