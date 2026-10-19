namespace BuurmansRgb.Contracts;

public interface IEngineConnection : IDisposable
{
    IEngineApi Api { get; }

    // Raised on a background thread when the engine stops or the pipe breaks.
    event EventHandler? Disconnected;
}
