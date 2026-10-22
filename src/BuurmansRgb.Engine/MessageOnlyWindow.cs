namespace BuurmansRgb.Engine;

// A hidden window without a place on the screen, only for messages. Like SystemPowerEvents: create
// one on the tray thread, which keeps pumping messages until the engine stops, and dispose it
// there. The window is made in this constructor, before the constructor of the derived class has
// run, and WndProc may already get messages then: until it has, WndProc can only count on what
// field initializers set.
internal abstract class MessageOnlyWindow : NativeWindow, IDisposable
{
    private static readonly nint MessageOnlyParent = new(-3);

    protected MessageOnlyWindow() => CreateHandle(new CreateParams { Parent = MessageOnlyParent });

    // Gives back what the derived class took, while the window is still there, and then destroys it.
    public void Dispose()
    {
        Release();
        DestroyHandle();
    }

    protected abstract void Release();
}
