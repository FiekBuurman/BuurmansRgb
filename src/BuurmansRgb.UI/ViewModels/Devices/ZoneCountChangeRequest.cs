using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// The user asks for a change to the count of the LEDs of a zone (ChangeZoneCount), such as a pause
// or another LED; Change goes to the engine as it is.
public sealed record ZoneCountChangeRequest(string DeviceId, int ZoneIndex, ZoneCountChange Change);
