namespace BuurmansRgb.Engine.Profiles;

// Loads profiles by itself. EngineHost starts it once the lights are on, so what it loads is not
// overwritten by the saved lighting, and disposes it before the last save, so what it puts back
// when it stops is saved too.
internal interface IProfileTrigger : IDisposable
{
    void Start();
}
