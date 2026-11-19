namespace Duxel.Core;

/// <summary>Reuses compiled regular/filled button icons while keeping tab selection independent of hover.</summary>
public readonly record struct UiTabIcon
{
    private readonly UiCompiledIconPainter? _paint;
    internal UiTabIcon(UiCompiledIconPainter paint) => _paint = paint;
    public bool IsEmpty => _paint is null;

    public static UiTabIcon Create<TRegular, TFilled>()
        where TRegular : IUiButtonIcon where TFilled : IUiButtonIcon => new(UiCompiledIconPair<TRegular, TFilled>.Cached);

    public void Paint(UiControlContext context, UiRect bounds, UiColor color,
        bool selected, UiButtonInteraction interaction)
    {
        if (_paint is null) throw new InvalidOperationException("An empty tab icon cannot be painted.");
        _paint(context, bounds, color, selected, interaction);
    }

}
