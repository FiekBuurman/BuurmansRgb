namespace BuurmansRgb.Providers.OpenRgb;

internal interface IOpenRgbConnector
{
    // Throws when the server cannot be reached. A lost connection cannot be reused, so every
    // attempt asks for a new one.
    Task<IOpenRgbConnection> ConnectAsync(CancellationToken ct);
}
