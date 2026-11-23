namespace BuurmansRgb.UI.ViewModels;

// A click on an LED in the preview of one device; Extend means Shift was held, Toggle that Ctrl
// was: the LED joins the selection or leaves it, and the others stay.
public sealed record LedHit(int Index, bool Extend, bool Toggle = false);
