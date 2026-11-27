namespace BuurmansRgb.Engine.Lighting;

// The lighting to save and when it changed: all that saving the settings needs of the controller.
internal interface ILightingStateSource
{
    // Raised when the lighting was changed and should be saved.
    event EventHandler? LightingChanged;

    LightingState State { get; }
}