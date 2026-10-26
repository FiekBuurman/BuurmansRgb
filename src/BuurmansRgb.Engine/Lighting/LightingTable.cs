using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

// The lighting of every device: a global lighting plus the devices that differ from it. Until
// the lighting is applied or first changed, everything counts as the default and IsSet is false.
// Rejects a brightness outside 0 to 1, a speed outside EffectSpeed, a direction outside
// EffectDirection, a color loop or own colors of an effect that do not fit (ColorLoop.IsValid,
// EffectColors.IsValid) and an unknown effect with ArgumentException, before it changes anything. Not thread-safe: LightingController uses it under its lock.
internal sealed class LightingTable
{
    private readonly Dictionary<string, DeviceLighting> _devices = [];
    private DeviceLighting? _global;

    public bool IsSet => _global is not null;

    public DeviceLighting Global => _global ?? DeviceLighting.Default;

    public DeviceLighting Get(string deviceId) => _devices.GetValueOrDefault(deviceId, Global);

    // Only the devices that differ from the global lighting.
    public IReadOnlyDictionary<string, DeviceLighting> Snapshot() => new Dictionary<string, DeviceLighting>(_devices);

    public void Replace(DeviceLighting global, IReadOnlyDictionary<string, DeviceLighting> devices)
    {
        Validate(global);
        foreach (var lighting in devices.Values)
        {
            Validate(lighting);
        }

        _global = global;
        _devices.Clear();
        foreach (var (deviceId, lighting) in devices)
        {
            _devices[deviceId] = lighting;
        }
    }

    // A global change also applies to every device with its own lighting, so a global color keeps
    // a device's own brightness and the other way round.
    public void SetGlobal(Func<DeviceLighting, DeviceLighting> change)
    {
        var global = change(Global);
        Validate(global);
        var devices = _devices.Select(pair => (DeviceId: pair.Key, Lighting: change(pair.Value))).ToArray();
        foreach (var (_, lighting) in devices)
        {
            Validate(lighting);
        }

        _global = global;
        foreach (var (deviceId, lighting) in devices)
        {
            SetValidated(deviceId, lighting);
        }
    }

    public void Set(string deviceId, DeviceLighting lighting)
    {
        Validate(lighting);
        PinGlobal();
        SetValidated(deviceId, lighting);
    }

    // The parameter names are those of the IPC calls, so the UI gets the same error as before.
    private static void Validate(DeviceLighting lighting)
    {
        if (!(lighting.Brightness is >= 0.0 and <= 1.0))
        {
            throw new ArgumentOutOfRangeException("brightness", lighting.Brightness, "Brightness runs from 0 to 1.");
        }

        if (!EffectSpeed.IsValid(lighting.Speed))
        {
            throw new ArgumentOutOfRangeException("speed", lighting.Speed, $"Speed runs from {EffectSpeed.Min} to {EffectSpeed.Max}.");
        }

        if (!EffectDirection.IsValid(lighting.Direction))
        {
            throw new ArgumentOutOfRangeException(
                "degrees", lighting.Direction, $"A direction runs from {EffectDirection.Min} to {EffectDirection.Max} degrees.");
        }

        if (!lighting.Loop.IsValid)
        {
            throw new ArgumentException($"A color loop has {ColorLoop.MinColors} to {ColorLoop.MaxColors} colors.", "colors");
        }

        if (!lighting.EffectColors.IsValid)
        {
            throw new ArgumentException(
                $"Only twinkle, comet, scanner and fire have colors of their own, {EffectColors.MinColors} to {EffectColors.MaxColors} of them.", "colors");
        }

        EffectCatalog.ThrowIfUnknown(lighting.EffectId);
    }

    // A device can only differ from a global lighting that is set, so the default becomes the
    // global lighting the first time one device gets its own.
    private void PinGlobal() => _global = Global;

    // A device that ends up equal to the global lighting follows it again, which keeps the
    // settings file to the devices that really differ.
    private void SetValidated(string deviceId, DeviceLighting lighting)
    {
        if (lighting == Global)
        {
            _devices.Remove(deviceId);
        }
        else
        {
            _devices[deviceId] = lighting;
        }
    }
}
