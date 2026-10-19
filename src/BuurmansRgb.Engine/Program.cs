namespace BuurmansRgb.Engine;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Tray icon, render loop and IPC server follow in task 1 and 2.
        ApplicationConfiguration.Initialize();
    }
}
