using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb;

// A device as the engine sees it, plus where to send its colors on the server.
internal sealed record MappedDevice(RgbDevice Device, int Index);