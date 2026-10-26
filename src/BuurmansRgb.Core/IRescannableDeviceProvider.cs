namespace BuurmansRgb.Core;

// A provider that can search for its devices again, for example when a device that was switched
// off and on again does not come back by itself. The new list follows through DevicesChanged.
// RequestRescanAsync completes once the search is over and the provider has the new list, with
// false when the source of the devices could not be reached, such as a server that does not run.
// A request while a search is on its way gets the answer of that search.
public interface IRescannableDeviceProvider : IDynamicDeviceProvider
{
    Task<bool> RequestRescanAsync(CancellationToken ct);
}
