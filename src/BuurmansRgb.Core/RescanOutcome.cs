namespace BuurmansRgb.Core;

// What a search for the devices again came to (IRescannableDeviceProvider.RequestRescanAsync).
// The number goes over the pipe to the settings app, so new values go at the end.
public enum RescanOutcome
{
    // The source searched, and its new list came; it may still find more devices later.
    Rescanned,

    // The source of the devices could not be reached, such as a server that does not run.
    Unreachable,

    // The source was reached and searched, but did not give its new list in time; the provider
    // connects anew by itself.
    ListNotLoaded,
}
