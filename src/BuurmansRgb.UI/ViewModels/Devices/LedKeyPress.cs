using BuurmansRgb.Core;

namespace BuurmansRgb.UI.ViewModels;

// A key in the preview of a zone; Extend means Shift was held. For Up and Down, Target is the LED
// of the zone in the row above or below the active one as the view draws it, counted from the
// start of the zone, or LedMatrix.NoLed when the zone has no such row.
public sealed record LedKeyPress(LedKey Key, bool Extend = false, int Target = LedMatrix.NoLed);
