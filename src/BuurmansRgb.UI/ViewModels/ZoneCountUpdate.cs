using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// A change to the count of the LEDs of a zone, such as a pause or another LED.
public sealed record ZoneCountUpdate(string DeviceId, int ZoneIndex, ZoneCountChange Change);
