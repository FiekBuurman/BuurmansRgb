namespace BuurmansRgb.Core;

// Where a device is connected, as its provider tells it, so two devices of the same model can be
// told apart: the port it is on, such as an SMBus or the path of a USB device, and its address on
// that port when it has one, such as "0x52" for a RAM stick.
public sealed record DeviceLocation(string Port, string? Address = null);
