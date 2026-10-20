namespace BuurmansRgb.Core;

// Errors that providers and the engine give in the same words.
public static class DeviceErrors
{
    public static ArgumentException Unknown(string deviceId) => new($"Unknown device '{deviceId}'.", nameof(deviceId));
}