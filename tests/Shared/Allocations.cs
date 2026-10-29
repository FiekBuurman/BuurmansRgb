namespace BuurmansRgb.Tests;

// The bytes this thread allocates while action runs, for the tests that check that a frame
// allocates nothing. GC.GetAllocatedBytesForCurrentThread counts what is left of the thread's
// allocation context as allocated when a background GC, started by another test of the process,
// takes that context away; that adds up to 8 KB that were never allocated. A full collection first
// leaves this thread without an allocation context, so there is nothing to take away until action
// allocates itself. That protects only an action that allocates nothing: once it allocates, the
// thread gets a fresh context of 8 KB in the window, and a GC of another test can still count the
// unused part of it. A test whose action allocates within a budget therefore takes the least of a
// few windows, each with fresh input.
internal static class Allocations
{
    public static long During(Action action)
    {
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
