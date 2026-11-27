namespace BuurmansRgb.UI.Services;

// A process that shows a window now, as RunningPrograms gets it: its id, its name as Windows gives
// it ("olk", without ".exe") and the title of its main window, empty when it has none.
internal readonly record struct ProcessWindow(int Id, string Name, string Title);
