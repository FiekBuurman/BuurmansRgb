namespace BuurmansRgb.Providers.OpenRgb;

internal interface IOpenRgbConnector
{
    // Throws OpenRgbUnreachableException when nothing answers at the address of the server, and
    // another exception when something answers but connecting fails. A lost connection cannot be
    // reused, so every attempt asks for a new one.
    Task<IOpenRgbConnection> ConnectAsync(CancellationToken ct);
}
