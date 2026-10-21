using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

// Nothing is in front until a test brings a program there; Follow only says what was asked.
internal sealed class FakeForegroundWindow : IForegroundWindow
{
    public string? Program { get; private set; }

    public bool Following { get; private set; }

    public int FollowCalls { get; private set; }

    public event EventHandler<string?>? Changed;

    public int Listeners => Changed?.GetInvocationList().Length ?? 0;

    public void Follow(bool on)
    {
        FollowCalls++;
        Following = on;
    }

    // As Windows tells it: a window of program came to the front (null for one without a name).
    public void Bring(string? program)
    {
        Program = program;
        Changed?.Invoke(this, program);
    }
}
