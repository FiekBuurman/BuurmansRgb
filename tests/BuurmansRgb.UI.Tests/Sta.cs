using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Tests;

// WPF elements want a single-threaded apartment, which the threads of the test runner are not.
internal static class Sta
{
    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                // A dispatcher left running when its thread ends keeps its weak event table, which
                // at process exit asks that thread, gone by then, to clean up and waits for it in
                // vain. With a few hundred UI tests those waits outlast the ten seconds the test
                // runner gives the process to exit.
                Dispatcher.FromThread(Thread.CurrentThread)?.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
