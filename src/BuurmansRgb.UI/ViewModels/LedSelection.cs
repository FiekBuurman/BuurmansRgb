namespace BuurmansRgb.UI.ViewModels;

// The LEDs First to Last (both included, counted from 0) of one device.
public sealed record LedSelection(string DeviceId, string DeviceName, int First, int Last)
{
    // First and Last of a preview that shows no selection.
    public const int NoSelection = -1;

    public int Count => Last - First + 1;
}
