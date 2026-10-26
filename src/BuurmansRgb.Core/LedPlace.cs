namespace BuurmansRgb.Core;

// Where an LED sits in its zone, from 0 to 1: X from left to right and Y from top to bottom, as
// the preview draws it. A zone fits in a square of 1 by 1 with its own shape kept, so a key map
// twice as wide as high runs from 0 to 1 across and from 0 to 0.5 down.
public readonly record struct LedPlace(double X, double Y);
