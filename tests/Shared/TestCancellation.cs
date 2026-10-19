namespace BuurmansRgb.Tests;

// The cancellation token of the running test under a short name, imported into every test
// project with a static using.
internal static class TestCancellation
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;
}