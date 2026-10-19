namespace BuurmansRgb.Engine.AutoStart;

internal interface IAutoStart
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
