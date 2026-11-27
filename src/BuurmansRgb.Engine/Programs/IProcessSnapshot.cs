namespace BuurmansRgb.Engine.Programs;

// All processes that run now, of every user and the services too, for the check on other RGB
// software (OtherRgbSoftwareCheck). Taken only when asked, never on a timer.
internal interface IProcessSnapshot
{
    IReadOnlyList<RunningProcess> Take();
}
