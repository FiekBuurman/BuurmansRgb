namespace BuurmansRgb.Contracts;

// Sequence grows with every frame, so a client can drop a frame that arrives late.
public sealed record LightingFrame(long Sequence, IReadOnlyList<DeviceFrame> Devices)
{
    public static LightingFrame Empty { get; } = new(0, []);
}
