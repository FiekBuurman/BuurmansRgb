namespace BuurmansRgb.Contracts;

// What IEngineApi.ImportSharedAsync did, and why a code could not be read (ShareCode.TryDecode).
// Only Imported added anything.
public enum ShareImportOutcome
{
    Imported,

    // Not JSON, not an object, without "version" and "kind", or a code that was cut off or changed.
    NotShared,

    // From a newer version of the app, with a "version" this engine does not know.
    NewerVersion,

    // Larger than SharedFiles.MaxJsonBytes, or a code longer than ShareCode.MaxLength.
    TooLarge,

    // The JSON is of the app, but something in it is not right: a field the engine does not know,
    // a value of the wrong type or out of range, a name that is too long or like a path, or more
    // devices or effects than SharedFiles allows.
    Invalid,

    // A scene of some devices of which none is here, by id or by type and name: it would do nothing.
    NoDevices,

    // The engine keeps CustomEffects.MaxEffects own effects already, or would with those of the
    // scene.
    TooManyEffects,
}
