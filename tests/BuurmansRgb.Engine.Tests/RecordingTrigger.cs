using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Tests;

// A profile trigger that runs a test's code when the engine starts and, if given, when it stops.
internal sealed class RecordingTrigger(Action started, Action? disposed = null) : IProfileTrigger
{
    public void Start() => started();

    public void Dispose() => disposed?.Invoke();
}
