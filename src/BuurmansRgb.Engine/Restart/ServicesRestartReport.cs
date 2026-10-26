using System.Globalization;

namespace BuurmansRgb.Engine.Restart;

// What became of one service in "Herstarten".
internal enum ServiceOutcome
{
    Restarted = 1,

    // Not on this pc as a service (OpenRGB) or driver (PawnIO).
    NotInstalled = 2,

    // Left as it was: PawnIO did not stop (not stoppable, or in use by another program).
    NotStopped = 3,

    // Stopping or starting went wrong, or the step with admin rights failed as a whole.
    Failed = 4,

    // The user said no to the UAC prompt.
    Declined = 5,

    // PawnIO left as it was without trying, as OpenRGB, which holds it, did not stop first.
    SkippedForOpenRgb = 6,
}

// The outcome of the step with admin rights. It comes back to the engine as the exit code of that
// step (no file an elevated process writes where the user can write too), and goes to the new
// engine on its command line after --restarted, so that one tells what restarted.
internal readonly record struct ServicesRestartReport(ServiceOutcome OpenRgb, ServiceOutcome PawnIo)
{
    // Marks an exit code as one of this step, so a crash (an NTSTATUS such as 0xE0434352, or 1)
    // never reads as a result.
    private const int CodeMark = 0x42520000;
    private const int CodeMarkMask = unchecked((int)0xFFFF0000);

    public static ServicesRestartReport Declined { get; } = new(ServiceOutcome.Declined, ServiceOutcome.Declined);

    public static ServicesRestartReport Failed { get; } = new(ServiceOutcome.Failed, ServiceOutcome.Failed);

    public static ServicesRestartReport NothingInstalled { get; } = new(ServiceOutcome.NotInstalled, ServiceOutcome.NotInstalled);

    public int ToExitCode() => CodeMark | ((int)OpenRgb << 8) | (int)PawnIo;

    // Anything that is not an exit code of this step counts as a failed step.
    public static ServicesRestartReport FromExitCode(int code)
    {
        if ((code & CodeMarkMask) != CodeMark)
        {
            return Failed;
        }

        var openRgb = (ServiceOutcome)((code >> 8) & 0xFF);
        var pawnIo = (ServiceOutcome)(code & 0xFF);
        return Enum.IsDefined(openRgb) && Enum.IsDefined(pawnIo) ? new(openRgb, pawnIo) : Failed;
    }

    public string ToArgument() => ToExitCode().ToString(CultureInfo.InvariantCulture);

    public static ServicesRestartReport? ParseArgument(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? FromExitCode(code) : null;
}
