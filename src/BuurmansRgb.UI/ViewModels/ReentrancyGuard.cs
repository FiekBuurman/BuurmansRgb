namespace BuurmansRgb.UI.ViewModels;

// Marks a stretch of code, so change handlers it triggers can tell they come from it and not from
// the user. Runs may nest; IsActive stays true until the outermost one ends. UI thread only.
internal sealed class ReentrancyGuard
{
    public bool IsActive { get; private set; }

    public void Run(Action action)
    {
        var wasActive = IsActive;
        IsActive = true;
        try
        {
            action();
        }
        finally
        {
            IsActive = wasActive;
        }
    }
}
