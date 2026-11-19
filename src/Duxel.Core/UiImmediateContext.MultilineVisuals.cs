namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private UiRect PrepareMultilineVisual(in UiMultilineInputLayout layout, string text, UiVisualTextLines visual,
        int caret, string? composition, bool pointerHitPhase = false)
    {
        var content = layout.ContentBounds;
        var rightInset = layout.Field.Width - (content.X - layout.Field.X) - content.Width;
        var gutter = Math.Clamp(MathF.Min(ScrollbarSize, layout.Field.Width) - rightInset, 0, content.Width);
        var bottomInset = layout.Field.Height - (content.Y - layout.Field.Y) - content.Height;
        var horizontalGutter = Math.Clamp(MathF.Min(ScrollbarSize, layout.Field.Height) - bottomInset, 0, content.Height);
        var activeSource = !layout.Options.ReadOnly && layout.StyledEditorId is { } editorId && (_state.ActiveId == editorId || _state.FocusedId == editorId)
            ? caret : -1;
        // Hit testing and held selection must use the same source/display map.
        // Reveal the new active block after the pointer gesture completes.
        var preserveActiveBlock = !layout.Options.ReadOnly && layout.StyledEditorId is { } pointerOwner
            && (pointerHitPhase && _leftMousePressed && IsHovering(IntersectRect(CurrentClipRect, layout.Field))
                || _leftMouseDown && !_leftMouseReleased && _state.OwnsTextDrag(pointerOwner));
        if (!string.IsNullOrEmpty(composition))
        {
            var display = string.Concat(text.AsSpan(0, caret), composition, text.AsSpan(caret));
            visual.Composition.Prepare(this, display, content.Width, content.Height, layout.LineHeight, gutter, layout.Wrap, horizontalGutter,
                layout.StyledSource?.Composition, activeSource < 0 ? -1 : caret + composition.Length, preserveActiveBlock);
            visual.Prepare(this, text, visual.Composition.EffectiveWidth, visual.Composition.EffectiveHeight, layout.LineHeight, 0, layout.Wrap,
                styled: layout.StyledSource, activeSourceIndex: activeSource, preserveActiveBlock: preserveActiveBlock);
        }
        else
        {
            visual.ClearComposition();
            visual.Prepare(this, text, content.Width, content.Height, layout.LineHeight, gutter, layout.Wrap, horizontalGutter,
                layout.StyledSource, activeSource, preserveActiveBlock);
        }
        return content with
        {
            Width = visual.EffectiveWidth,
            Height = visual.EffectiveHeight
        };
    }

    private static float EnsureVisualCaretHorizontal(UiVisualTextLines lines, int index, float offset, float width)
    {
        var x = lines.X(index, lines.FindLine(index));
        if (x < offset)
            return x;
        return x > offset + width ? x - width : offset;
    }

    private static int HitMultilineVisual(UiVisualTextLines visual, int caret, string? composition,
        float x, float y, float lineHeight)
    {
        if (string.IsNullOrEmpty(composition))
            return visual.Hit(x, y, lineHeight);
        var hit = visual.Composition.Hit(x, y, lineHeight);
        visual.ClearAffinity();
        return new UiCompositionTextMap(caret, composition.Length).Source(hit);
    }

    private static UiVector2 VisualCaret(UiVisualTextLines visual, int caret, UiVector2 origin, float lineHeight)
    {
        var line = visual.FindLine(caret);
        return new(origin.X + visual.X(caret, line), origin.Y + visual.Y(line));
    }

    private void PaintVisualComposition(UiVisualTextLines visual, int start, int end,
        UiVector2 origin, float lineHeight, UiRect clip, UiColor color)
    {
        for (var line = 0; line < visual.Count; line++)
        {
            var row = visual[line];
            if (end <= row.Start || start >= row.End)
                continue;
            foreach (var fragment in visual.SelectionFragments(Math.Max(start, row.Start), Math.Min(end, row.End), line))
            {
                var rect = new UiRect(origin.X + fragment.Left, origin.Y + visual.Y(line) + visual.LineHeight(line) - 1,
                    fragment.Width, 1);
                if (rect.Width > 0)
                    _builder.AddRectFilled(rect, color, _whiteTexture, clip);
            }
        }
    }

    private void PaintVisualSelection(UiVisualTextLines visual, string text, UiTextSelection selection,
        UiVector2 origin, float lineHeight, UiRect clip, UiColor color)
    {
        var (start, end) = GetSelectionRange(selection);
        var bottom = clip.Y + clip.Height;
        for (var line = visual.LineAtY(MathF.Max(0, clip.Y - origin.Y)); line < visual.Count; line++)
        {
            var row = visual[line];
            var y = origin.Y + visual.Y(line) + 2;
            if (end <= row.Start || y >= bottom)
                break;
            if (start > row.End)
                continue;
            var height = MathF.Max(0, visual.LineHeight(line) - 4);
            if (height == 0 || y + height <= clip.Y)
                continue;
            foreach (var fragment in visual.SelectionFragments(Math.Max(start, row.Start), Math.Min(end, row.End), line))
            {
                var rect = new UiRect(origin.X + fragment.Left, y, fragment.Width, height);
                if (rect.Width > 0)
                    _builder.AddRectFilled(rect, color, _whiteTexture, clip);
            }
            if (end > row.End && row.End < text.Length && text[row.End] is '\r' or '\n')
            {
                var x = visual.X(row.End, line);
                var rect = new UiRect(origin.X + x, y,
                    MathF.Max(0, MathF.Min(clip.Width - x, MeasureTextWidthSpan(" "))), height);
                if (rect.Width > 0)
                    _builder.AddRectFilled(rect, color, _whiteTexture, clip);
            }
        }
    }

    private void PaintVisualLines(UiVisualTextLines visual, UiVector2 origin, float lineHeight,
        UiColor color, UiRect clip, bool typography, UiTextSelection selection = default,
        UiColor? selectedForeground = null, UiStyledTextPaintPhase phase = UiStyledTextPaintPhase.All)
    {
        var visibleTop = MathF.Max(0, clip.Y - origin.Y);
        var visibleBottom = clip.Y + clip.Height;
        for (var line = Math.Max(0, visual.LineAtY(visibleTop) - 1); line < visual.Count; line++)
        {
            var y = origin.Y + visual.Y(line);
            if (y >= visibleBottom)
                break;
            if (y + visual.LineHeight(line) > clip.Y && y < clip.Y + clip.Height)
            {
                var (first, last) = GetSelectionRange(selection);
                var row = visual[line];
                var start = Math.Max(first, row.Start);
                var end = Math.Min(last, row.End);
                if (visual.StyledSource is not null)
                {
                    if (selectedForeground.HasValue && end > start)
                    {
                        var rowClip = IntersectRect(clip, new(clip.X, y, clip.Width, visual.LineHeight(line)));
                        foreach (var region in new UiTextSelectionPaintRegions(rowClip, origin.X, visual.SelectionFragments(start, end, line)))
                            PaintStyledLine(visual, line, origin, color, region.Clip,
                                new(region.Selected ? selectedForeground : null, phase));
                    }
                    else
                        PaintStyledLine(visual, line, origin, color, clip, new(null, phase));
                }
                else
                {
                    if (selectedForeground is { } selectedColor && end > start)
                        PaintSelectedInputRun(row.Text, new(origin.X, y), color, selectedColor,
                            IntersectRect(clip, new(clip.X, y, clip.Width, visual.LineHeight(line))), typography,
                            visual.SelectionFragments(start, end, line));
                    else
                        PaintInputRun(row.Text, new(origin.X, y), color, clip, typography);
                }
            }
        }
    }
}
