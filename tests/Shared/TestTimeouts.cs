namespace BuurmansRgb.Tests;

// How long a test waits for something that happens on another thread. Used by its qualified
// name: a static using would clash with System.Threading.Timeout.
internal static class TestTimeouts
{
    public static readonly TimeSpan Short = TimeSpan.FromSeconds(5);

    // For a real socket server that has to start, connect and answer first.
    public static readonly TimeSpan Long = TimeSpan.FromSeconds(10);
}
