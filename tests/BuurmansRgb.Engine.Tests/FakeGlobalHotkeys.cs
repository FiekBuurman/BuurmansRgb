using System.Windows.Forms;
using BuurmansRgb.Engine.Hotkeys;

namespace BuurmansRgb.Engine.Tests;

// Hotkeys without Windows: Press does what the real key would, and Taken plays another app that
// has a key already.
internal sealed class FakeGlobalHotkeys : IGlobalHotkeys
{
    private readonly Dictionary<Keys, Action> _registered = [];

    public HashSet<Keys> Taken { get; } = [];

    public IReadOnlyCollection<Keys> Registered => _registered.Keys;

    public int RegisterCount { get; private set; }

    public bool Register(Keys key, Action pressed)
    {
        RegisterCount++;
        if (Taken.Contains(key))
        {
            return false;
        }

        _registered[key] = pressed;
        return true;
    }

    public void Unregister(Keys key) => _registered.Remove(key);

    public bool IsRegistered(Keys key) => _registered.ContainsKey(key);

    // Like Windows: a key that is not registered (any more) does nothing.
    public void Press(Keys key)
    {
        if (_registered.TryGetValue(key, out var pressed))
        {
            pressed();
        }
    }
}
