namespace BuurmansRgb.UI.ViewModels;

// A mode in a ComboBox of Instellingen. Its Dutch name comes from Strings.resx
// ("<enum type>_<mode>", such as "PausedLightsMode_Dim"); a mode without one shows its English name.
public sealed record EnumOption<TEnum>(TEnum Mode, string DisplayName)
    where TEnum : struct, Enum
{
    public static EnumOption<TEnum> Create(TEnum mode) =>
        new(mode, UiText.Lookup(typeof(TEnum).Name, mode) ?? mode.ToString());

    // A screen reader names a ComboBox item by its ToString, not by DisplayMemberPath.
    public override string ToString() => DisplayName;
}
