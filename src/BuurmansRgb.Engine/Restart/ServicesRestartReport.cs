using System.ComponentModel;
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
//
// The exit code, from the highest bit: 8 bits mark (0x42), 4 bits OpenRGB, 4 bits PawnIO, and the
// cause of the first failure (ServicesRestartCause) in the lowest 16 bits: 3 bits step, 2 bits
// ServiceControlFailure and 11 bits Win32 error.
internal readonly record struct ServicesRestartReport(ServiceOutcome OpenRgb, ServiceOutcome PawnIo)
{
    // Marks an exit code as one of this step, so a crash (an NTSTATUS such as 0xE0434352, or 1)
    // never reads as a result.
    private const int CodeMark = 0x42000000;
    private const int CodeMarkMask = unchecked((int)0xFF000000);

    private const int OpenRgbShift = 20;
    private const int PawnIoShift = 16;
    private const int OutcomeMask = 0xF;
    private const int StepShift = 13;
    private const int StepMask = 0x7;
    private const int FailureShift = 11;
    private const int FailureMask = 0x3;
    private const int Win32ErrorMask = 0x7FF;

    public static ServicesRestartReport Declined { get; } = new(ServiceOutcome.Declined, ServiceOutcome.Declined);

    public static ServicesRestartReport Failed { get; } = new(ServiceOutcome.Failed, ServiceOutcome.Failed);

    public static ServicesRestartReport NothingInstalled { get; } = new(ServiceOutcome.NotInstalled, ServiceOutcome.NotInstalled);

    // With the cause of the first failure in the step, which the engine logs; a Win32 error that
    // does not fit in 11 bits goes as 0.
    public int ToExitCode(ServicesRestartCause cause = default)
    {
        var code = CodeMark | ((int)OpenRgb << OpenRgbShift) | ((int)PawnIo << PawnIoShift);
        if (cause.Step == ServicesRestartStep.None)
        {
            return code;
        }

        var win32Error = cause.Win32Error is > 0 and <= Win32ErrorMask ? cause.Win32Error : 0;
        return code | ((int)cause.Step << StepShift) | (((int)cause.Failure & FailureMask) << FailureShift) | win32Error;
    }

    // Anything that is not an exit code of this step counts as a failed step.
    public static ServicesRestartReport FromExitCode(int code)
    {
        if (!IsOfTheStep(code))
        {
            return Failed;
        }

        var openRgb = (ServiceOutcome)((code >> OpenRgbShift) & OutcomeMask);
        var pawnIo = (ServiceOutcome)((code >> PawnIoShift) & OutcomeMask);
        return Enum.IsDefined(openRgb) && Enum.IsDefined(pawnIo) ? new(openRgb, pawnIo) : Failed;
    }

    // The cause in an exit code of this step; none for another code.
    public static ServicesRestartCause CauseFromExitCode(int code)
    {
        var step = (ServicesRestartStep)((code >> StepShift) & StepMask);
        if (!IsOfTheStep(code) || step == ServicesRestartStep.None || !Enum.IsDefined(step))
        {
            return default;
        }

        return new(step, (ServiceControlFailure)((code >> FailureShift) & FailureMask), code & Win32ErrorMask);
    }

    public string ToArgument() => ToExitCode().ToString(CultureInfo.InvariantCulture);

    public static ServicesRestartReport? ParseArgument(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? FromExitCode(code) : null;

    private static bool IsOfTheStep(int code) => (code & CodeMarkMask) == CodeMark;
}

// Which part of the step with admin rights went wrong first.
internal enum ServicesRestartStep
{
    None = 0,
    StopOpenRgb = 1,
    StopPawnIo = 2,
    StartPawnIo = 3,
    StartOpenRgb = 4,

    // The step as a whole, such as looking up the services.
    Step = 5,
}

// Why the step with admin rights did not restart everything: the first failure, for the log of the
// engine, as the step itself writes no log. Win32Error is 0 when there is none, such as for a
// timeout, or when it does not fit in the exit code.
internal readonly record struct ServicesRestartCause(ServicesRestartStep Step, ServiceControlFailure Failure, int Win32Error)
{
    public static ServicesRestartCause From(ServicesRestartStep step, Exception exception) =>
        new(step, exception is ServiceControlException control ? control.Failure : ServiceControlFailure.Other, ServiceControl.Win32Error(exception));

    public override string ToString()
    {
        var what = Step switch
        {
            ServicesRestartStep.None => "nothing",
            ServicesRestartStep.StopOpenRgb => "stopping OpenRGB",
            ServicesRestartStep.StopPawnIo => "stopping PawnIO",
            ServicesRestartStep.StartPawnIo => "starting PawnIO",
            ServicesRestartStep.StartOpenRgb => "starting OpenRGB",
            _ => "the step itself",
        };
        return Win32Error == 0
            ? $"{what}: {Failure}"
            : $"{what}: {Failure}, Win32 error {Win32Error} ({new Win32Exception(Win32Error).Message})";
    }
}
