namespace BuurmansRgb.UI.Services;

// Reads nothing: for a MainViewModel without a meter, such as in a test that is not about it.
public sealed class NoProcessMeter : IProcessMeter
{
    public static NoProcessMeter Instance { get; } = new();

    private NoProcessMeter()
    {
    }

    public ProcessReading? Read(int processId) => null;

    public void Release()
    {
    }
}
