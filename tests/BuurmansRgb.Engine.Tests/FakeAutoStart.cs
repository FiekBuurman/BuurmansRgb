using BuurmansRgb.Engine.AutoStart;

namespace BuurmansRgb.Engine.Tests;

// Autostart without the registry, so no test changes what Windows starts on this pc.
internal sealed class FakeAutoStart : IAutoStart
{
    private bool _enabled;

    public Exception? SetEnabledException { get; set; }

    // Thrown when IsEnabled is read, as when the Run key cannot be read.
    public Exception? IsEnabledException { get; set; }

    public int SetEnabledCount { get; private set; }

    public bool IsEnabled
    {
        get => IsEnabledException is not null ? throw IsEnabledException : _enabled;
        set => _enabled = value;
    }

    public void SetEnabled(bool enabled)
    {
        SetEnabledCount++;
        if (SetEnabledException is not null)
        {
            throw SetEnabledException;
        }

        _enabled = enabled;
    }

    public string? FollowMoveResult { get; set; }

    public Exception? FollowMoveException { get; set; }

    public string? FollowMove() => FollowMoveException is not null ? throw FollowMoveException : FollowMoveResult;
}
