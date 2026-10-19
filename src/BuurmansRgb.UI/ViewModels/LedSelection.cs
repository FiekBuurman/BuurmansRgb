namespace BuurmansRgb.UI.ViewModels;

// The LEDs First to Last (both included, counted from 0) of one device.
public sealed record LedSelection(string DeviceId, string DeviceName, int First, int Last)
{
    public int Count => Last - First + 1;
}
