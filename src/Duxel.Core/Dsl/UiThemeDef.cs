namespace Duxel.Core.Dsl;

/// <summary>
/// Parsed representation of a .duxel-theme file.
/// </summary>
public sealed class UiThemeDef(string name, string? basePreset, List<UiThemeColorEntry> overrides)
{
    public string Name { get; } = name;
    public string? BasePreset { get; } = basePreset;
    public IReadOnlyList<UiThemeColorEntry> Overrides { get; } = overrides;
}

public readonly record struct UiThemeColorEntry(UiStyleColor Color, UiColor Value);
