namespace BuurmansRgb.UI.ViewModels;

// The chosen LEDs of one device, counted from 0: never none. First and Last are the lowest and the
// highest of them; with Ctrl+click there can be LEDs between them that are not chosen (IsRange).
public sealed record LedSelection(string DeviceId, string DeviceName, LedSet Leds)
{
    // First and Last of a preview that shows no selection.
    public const int NoSelection = -1;

    // The LEDs first to last, both included.
    public LedSelection(string deviceId, string deviceName, int first, int last)
        : this(deviceId, deviceName, LedSet.Range(first, last))
    {
    }

    public int First => Leds.First;

    public int Last => Leds.Last;

    public int Count => Leds.Count;

    public bool IsRange => Leds.IsRange;

    public bool Contains(int led) => Leds.Contains(led);
}
