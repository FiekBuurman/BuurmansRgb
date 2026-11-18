namespace BuurmansRgb.Contracts;

// What IEngineApi.ImportSharedAsync did. With Imported: Kind, and Name, the name the scene or own
// effect got here, which is SharedName with " (2)" or another number after it when a scene or
// effect of that name was there already (nothing is overwritten). AlreadyThere for an own effect
// that was here already with the same name and steps, so none was added. UnmatchedDevices names
// the devices of a scene that are not here, by id or by type and name, whose lighting was left
// out; AddedEffects the own effects a scene brought along, by the names they got here.
public sealed record ShareImportResult(
    ShareImportOutcome Outcome,
    SharedKind Kind = SharedKind.Scene,
    string? Name = null,
    string? SharedName = null,
    bool AlreadyThere = false,
    IReadOnlyList<string>? UnmatchedDevices = null,
    IReadOnlyList<string>? AddedEffects = null)
{
    public static ShareImportResult Refused(ShareImportOutcome outcome) => new(outcome);

    public bool IsRenamed => Name is not null && SharedName is not null && Name != SharedName;
}
