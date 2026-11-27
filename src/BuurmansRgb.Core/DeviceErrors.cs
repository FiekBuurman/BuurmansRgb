namespace BuurmansRgb.Core;

// Errors that providers and the engine give in the same words.
public static class DeviceErrors
{
    public static ArgumentException Unknown(string deviceId) => new($"Unknown device '{deviceId}'.", nameof(deviceId));

    // For a zone whose length cannot be set; zoneIndex is the argument that named the zone.
    public static ArgumentException NotResizable(RgbZone zone, string deviceId) =>
        new($"Zone '{zone.Name}' of device '{deviceId}' cannot be resized.", "zoneIndex");
}