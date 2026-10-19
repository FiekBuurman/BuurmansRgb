using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts;

public sealed record DeviceInfo(string Id, string Name, DeviceType Type, int LedCount);
