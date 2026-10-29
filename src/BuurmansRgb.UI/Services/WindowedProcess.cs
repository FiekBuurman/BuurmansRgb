namespace BuurmansRgb.UI.Services;

// A process that shows a window now, as WindowedPrograms gets it: its id, its name as Windows gives
// it ("olk", without ".exe") and the title of its main window, empty when it has none.
internal readonly record struct WindowedProcess(int Id, string Name, string Title);
