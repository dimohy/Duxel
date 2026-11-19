namespace Duxel.Core;

internal delegate void UiCompiledIconPainter(UiControlContext context, UiRect bounds, UiColor color,
    bool filled, UiButtonInteraction interaction);

internal static class UiCompiledIconPair<TRegular, TFilled>
    where TRegular : IUiButtonIcon where TFilled : IUiButtonIcon
{
    internal static readonly UiCompiledIconPainter Cached = Paint;
    private static void Paint(UiControlContext context, UiRect bounds, UiColor color, bool filled, UiButtonInteraction interaction)
    {
        if (filled) TFilled.Paint(context, bounds, color, interaction);
        else TRegular.Paint(context, bounds, color, interaction);
    }
}
