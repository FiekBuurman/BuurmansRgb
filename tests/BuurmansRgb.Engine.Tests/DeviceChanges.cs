using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

internal static class DeviceChanges
{
    // Gives the provider other devices, like OpenRGB starting or stopping, and waits until the
    // controller has taken them in.
    public static async Task ChangeDevicesAsync(this LightingController controller, DynamicDeviceProvider provider, params IReadOnlyList<RgbDevice> devices)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs e) => changed.TrySetResult();
        controller.DevicesChanged += OnChanged;
        try
        {
            provider.SetDevices(devices);
            await changed.Task.WaitAsync(TestTimeouts.Short, Ct);
        }
        finally
        {
            controller.DevicesChanged -= OnChanged;
        }
    }
}
