namespace BuurmansRgb.Providers.OpenRgb;

// Nothing accepted a connection at the address of the OpenRGB server, so it most likely does not
// run. Any other failure to connect means that something did answer.
internal sealed class OpenRgbUnreachableException(string message, Exception innerException) : Exception(message, innerException);
