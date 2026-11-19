namespace Duxel.Core;

public enum UiTreeSize
{
    Medium, Small
}
public enum UiTreeAppearance
{
    Subtle, SubtleAlpha, Transparent
}
public enum UiTreeTextOverflow
{
    Wrap, Ellipsis
}

public readonly record struct UiTreeOptions(UiTreeSize Size = UiTreeSize.Medium,
    UiTreeAppearance Appearance = UiTreeAppearance.Subtle, bool RightToLeft = false,
    float? Width = null, bool Navigation = false)
{
    public UiTreeTextOverflow TextOverflow
    {
        get; init;
    }
    internal bool Small => Size == UiTreeSize.Small;
    public bool ReducedMotion { get; init; }
    public bool RowActions { get; init; }
    /// <summary>Aligns the selection indicator and its hit area to the top of tall rows.</summary>
    public bool AlignSelectorToTop { get; init; }
    internal UiTreeOptions WithInheritedDirection(bool rightToLeft) => rightToLeft
        ? this with { RightToLeft = true } : this;
    internal void Validate()
    {
        if ((uint)Size > 1 || (uint)Appearance > 2 || (uint)TextOverflow > 1
            || Width is { } width && (!float.IsFinite(width) || width <= 0))
            throw new ArgumentOutOfRangeException(nameof(UiTreeOptions));
    }
}

public readonly record struct UiTreeMetrics(float MinimumRowHeight, float Indentation,
    float ChevronWidth = 24, float SelectorWidth = 24, float ContentPadding = 2)
{
    internal void Validate()
    {
        if (!float.IsFinite(MinimumRowHeight) || MinimumRowHeight <= 0
            || !float.IsFinite(Indentation) || Indentation < 0
            || !float.IsFinite(ChevronWidth) || ChevronWidth < 0
            || !float.IsFinite(SelectorWidth) || SelectorWidth < 0
            || !float.IsFinite(ContentPadding) || ContentPadding < 0)
            throw new InvalidOperationException("Tree presentation metrics must be finite and nonnegative, with positive row height.");
    }
}

public interface IUiTreePresentation
{
    static virtual bool CanRetainMeasurement => false;
    static virtual UiTreeLabel CreateLabel(string text, Func<UiTreeOptions> options)
        => throw new NotSupportedException("This Tree presentation requires explicit row content or a plain-label implementation.");
    static abstract UiTreeMetrics Measure(UiControlContext context, UiTreeOptions options);
    static abstract void PaintRow(UiControlContext context, UiRect bounds, UiRect chevron,
        UiButtonInteraction interaction, bool branch, bool expanded, bool selected, UiTreeOptions options);
    static virtual double ExpansionDurationSeconds => 0;
    static virtual bool PaintAnimatedRow(UiControlContext context, UiRect bounds, UiRect chevron,
        UiButtonInteraction interaction, bool branch, bool expanded, bool selected, UiTreeOptions options, float expansion)
        => false;
    static abstract void PaintSelector(UiControlContext context, UiRect bounds, UiButtonInteraction interaction,
        bool? value, bool multiple, UiTreeOptions options);
    static abstract void PaintFocus(UiControlContext context, UiRect bounds, UiTreeOptions options);
}
