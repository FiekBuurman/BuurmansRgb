namespace BuurmansRgb.UI.ViewModels;

// A click on an LED (see LedHit), with the device it was on. Anchor is where a range with Extend
// starts when no LED of the device is selected yet, such as the LED the keyboard moved away from;
// with LedSelection.NoSelection it starts at Index, as a first Shift+click does. Toggle, from
// Ctrl+click or Ctrl+Space, puts the LED in the selection or takes it out, keeping the others.
public sealed record LedClick(string DeviceId, int Index, bool Extend, int Anchor = LedSelection.NoSelection, bool Toggle = false);
