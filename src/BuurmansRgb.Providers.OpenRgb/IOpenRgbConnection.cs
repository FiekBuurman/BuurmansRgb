using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb;

// One connection to the OpenRGB SDK server. The calls may be made from several threads; any of
// them throws once the server is gone. Events are raised on a background thread.
internal interface IOpenRgbConnection : IDisposable
{
    // Raised once when the server closes the connection or stops. It may happen before anyone
    // subscribed, so check IsClosed after subscribing.
    event EventHandler? Closed;

    // Raised when the server rescanned its devices; the indexes may have shifted.
    event EventHandler? DeviceListChanged;

    bool IsClosed { get; }

    int ProtocolVersion { get; }

    Task<IReadOnlyList<OpenRgbDeviceData>> GetDevicesAsync(CancellationToken ct);

    // Lets the device take colors from the SDK: OpenRGB picks its Direct or Custom mode.
    void SetCustomMode(int deviceIndex);

    // Gives a zone another number of LEDs. The server sends no device list update for it, so the
    // caller reloads the devices to see the new length.
    void ResizeZone(int deviceIndex, int zoneIndex, int ledCount);

    // Asks the server to search for its devices again. It gives no answer: once its list changed,
    // DeviceListChanged follows, but only from a server that knows the request (OpenRGB 1.0 and
    // later); an older one ignores it.
    Task RequestRescanAsync(CancellationToken ct);

    // Returns false, without waiting and without sending, while another call still waits for the
    // server, such as a device reload. Callers may hold a lock, and that wait can last until the
    // request times out.
    bool TryUpdateLeds(int deviceIndex, ReadOnlySpan<RgbColor> colors);
}
