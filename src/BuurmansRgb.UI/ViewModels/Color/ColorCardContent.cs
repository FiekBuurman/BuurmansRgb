namespace BuurmansRgb.UI.ViewModels;

// What card 4 on Kleur shows, under the effect tiles: what belongs to the chosen effect (DESIGN.md
// § 9.2), or with chosen LEDs the color for those LEDs, whatever the effect.
public enum ColorCardContent
{
    // Statisch, Ademen, Golf and an effect the app does not know yet: the mixing panel.
    Color,

    // Kleurenloop: the colors of the loop, and the mixing panel for the chosen one.
    ColorLoop,

    // Fonkelen, Komeet, Scanner and Vuur: the colors of the effect, and the mixing panel for the chosen one.
    EffectColors,

    // An own effect, or "Eigen effect" without own effects: the choice of the own effects with
    // "+ Eigen effect maken", the editor of the chosen one and the mixing panel for its step.
    CustomEffect,

    // Per LED without chosen LEDs: where to choose them, and the mixing panel.
    PerLed,

    // Chosen LEDs, with any effect: which they are, Verloop vullen, Selectie opheffen and the mixing panel.
    Leds,

    // Regenboog, which makes its own colors: one sentence, no mixing panel.
    NoColor,
}
