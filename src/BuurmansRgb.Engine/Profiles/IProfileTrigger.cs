namespace BuurmansRgb.Engine.Profiles;

// Loads profiles by itself. Started once the lights are on, so what it loads is not overwritten
// by the saved lighting.
internal interface IProfileTrigger
{
    void Start();
}
