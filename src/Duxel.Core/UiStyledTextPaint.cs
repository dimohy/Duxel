namespace Duxel.Core;

internal enum UiStyledTextPaintPhase { All, Background, Content }

internal readonly record struct UiStyledTextPaint(UiColor? ForegroundOverride = null,
    UiStyledTextPaintPhase Phase = UiStyledTextPaintPhase.All)
{
    internal bool PaintBackground => Phase != UiStyledTextPaintPhase.Content;
    internal bool PaintContent => Phase != UiStyledTextPaintPhase.Background;
}
