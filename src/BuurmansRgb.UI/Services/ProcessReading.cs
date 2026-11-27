namespace BuurmansRgb.UI.Services;

// The CPU time the process used since it started, on all cores together, and its working set.
public readonly record struct ProcessReading(TimeSpan ProcessorTime, long WorkingSetBytes);
