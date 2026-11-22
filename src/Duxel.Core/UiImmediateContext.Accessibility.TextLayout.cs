using System.Globalization;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private UiAccessibilityTextLayout? CaptureInputTextLayout(string id, UiAccessibilityTextInfo? info,
        in UiInputLayout layout, in UiInputEditResult edit)
    {
        if (info is null)
            return null;
        var lineLayout = edit.LineLayout;
        lineLayout?.Prepare(this, edit.Text);
        if (lineLayout?.Geometry is null)
            lineLayout = null;
        if (edit.CompositionLayout is { } composition && edit.CompositionText is { Length: > 0 } preedit)
        {
            composition.Prepare(this, edit.Text, edit.CaretIndex, preedit);
            lineLayout = composition.Layout.Geometry is not null ? composition.Layout : null;
        }
        _accessibility!.CaptureTextViewport(id, layout.Content);
        return CaptureTextLayout(id, info.Content, new(layout.Content.X - edit.ScrollX, layout.Content.Y),
            IntersectRect(CurrentClipRect, layout.EditingBounds), layout.LineHeight,
            composition: edit.CompositionText is { Length: > 0 } text ? new(edit.CaretIndex, text, edit.CompositionWidth) : null,
            singleLine: lineLayout);
    }

    private UiAccessibilityTextLayout? CaptureMultilineTextLayout(string id, UiAccessibilityTextInfo? info,
        in UiMultilineInputLayout layout, in UiMultilineEditResult edit, bool reusePositionIndependentGeometry = false)
    {
        if (info is null)
            return null;
        var content = edit.VisualContent ?? layout.ContentBounds;
        _accessibility!.CaptureTextViewport(id, content);
        var lines = edit.CompositionLines ?? edit.VisualLines;
        if (lines is null)
        {
            // Custom composing snapshots must supply the same display map used by their painter.
            if (!string.IsNullOrEmpty(edit.CompositionText))
                return null;
            lines = _accessibility!.GetUnwrappedTextLines(id);
            lines.Prepare(this, info.Content.Text, content.Width, content.Height, layout.LineHeight, 0, wrap: false);
        }
        var clip = IntersectRect(CurrentClipRect, layout.Field);
        if (edit.VisualLines is not null)
            clip = IntersectRect(clip, content);
        return CaptureTextLayout(id, info.Content, new(content.X - edit.ScrollX, content.Y - edit.ScrollY),
            clip, layout.LineHeight, lines,
            edit.CompositionText is { Length: > 0 } text ? new(edit.CaretIndex, text, edit.CompositionWidth) : null,
            reusePositionIndependentGeometry: reusePositionIndependentGeometry);
    }

    private UiAccessibilityTextLayout CaptureTextLayout(string id, UiAccessibilityTextContent content, UiVector2 origin,
        UiRect clip, float height, UiVisualTextLines? visual = null, UiAccessibilityTextComposition? composition = null,
        UiSingleLineTextLayout? singleLine = null, bool reusePositionIndependentGeometry = false)
    {
        var key = LayoutMeasurementKey;
        UiAccessibilityTextVisualKey? visualKey = visual is null ? null : new(visual.EffectiveWidth, visual.Wrap, visual.StyledSource?.Projection);
        var previous = _accessibility!.TryGet(id, out var published) ? published.Node.TextLayout : null;
        if (reusePositionIndependentGeometry)
        {
            var clipped = TakeClippedCodeLayout(id);
            previous ??= clipped;
        }
        if (previous is not null && ReferenceEquals(previous.Content, content) && previous.MeasurementKey == key
            && previous.LineHeight == height
            && previous.VisualKey == visualKey && previous.VisualVersion == (visual?.Version ?? singleLine?.Version ?? 0)
            && previous.Composition == composition)
        {
            if (previous.Origin == origin && previous.ClipBounds == clip)
                return previous;
            if (reusePositionIndependentGeometry && previous.WithViewport(origin, clip) is { } moved)
                return moved;
        }
        var text = content.Text;
        var suffix = composition is { } composing ? text[composing.StartUtf16..] : null;
        int Display(int offset, bool end = false) => composition is { } c
            && (offset > c.StartUtf16 || offset == c.StartUtf16 && !end) ? offset + c.Text.Length : offset;
        int Committed(int offset) => composition is not { } c || offset <= c.StartUtf16 ? offset
            : offset <= c.StartUtf16 + c.Text.Length ? c.StartUtf16 : offset - c.Text.Length;
        float SingleX(int offset, bool end = false) => singleLine is not null ? origin.X
            + (composition is { } preedit && offset == preedit.StartUtf16 && !end
                ? singleLine.Geometry!.TrailingCaretX(Display(offset)) * singleLine.Scale : singleLine.X(Display(offset, end)))
            : composition is not { } c
            || offset < c.StartUtf16 || offset == c.StartUtf16 && end ? origin.X + (singleLine?.X(offset) ?? MeasureTextWidth(text, offset))
            : InputCompositionSuffixX(origin.X, text, c.StartUtf16, c.Width) + MeasureTextWidth(suffix!, offset - c.StartUtf16);
        var characters = new UiRect[content.CharacterCount];
        var carets = new UiRect[content.CharacterCount + 1];
        var lineRanges = new UiAccessibilityTextLine[visual?.Count ?? 1];
        for (var index = 0; index < lineRanges.Length; index++)
            lineRanges[index] = visual is null ? new(0, content.CharacterCount, content.CharacterCount)
                : new(content.GetCharacterOffset(Committed(visual[index].Start)), content.GetCharacterOffset(Committed(visual[index].End)), index + 1 < visual.Count
                    ? content.GetCharacterOffset(Committed(visual[index + 1].Start)) : content.CharacterCount);
        int Line(int index)
        {
            if (visual is null)
                return 0;
            var low = 0;
            var high = visual.Count;
            while (low + 1 < high)
            {
                var middle = low + (high - low) / 2;
                if (index >= visual[middle].Start)
                    low = middle;
                else
                    high = middle;
            }
            return low;
        }
        UiRect Caret(int index)
        {
            var display = Display(index);
            var line = Line(display);
            return visual is null ? new(SingleX(index), origin.Y, 0, height)
                : new(origin.X + visual.X(display, line), origin.Y + visual.Y(line), 0, visual.LineHeight(line));
        }
        var starts = StringInfo.ParseCombiningCharacters(text);
        for (var index = 0; index < starts.Length; index++)
        {
            var start = starts[index];
            var end = index + 1 < starts.Length ? starts[index + 1] : text.Length;
            var first = content.GetCharacterOffset(start);
            var last = content.GetCharacterOffset(end);
            var caret = Caret(start);
            var displayStart = visual?.Snap(Display(start)) ?? start;
            var displayEnd = visual?.SnapUp(Display(end, true)) ?? end;
            var line = Line(displayStart);
            var right = text[start] is '\r' or '\n' ? caret.X : visual is null
                ? SingleX(end, true) : origin.X + visual.X(displayEnd, line);
            var box = caret with
            {
                X = MathF.Min(caret.X, right),
                Width = MathF.Abs(right - caret.X)
            };
            if (singleLine is { Geometry: { } geometry })
            {
                var fragments = geometry.SelectionFragments(Display(start), Display(end, true));
                if (fragments.Length > 0)
                {
                    var left = fragments.Min(fragment => fragment.Left);
                    var edge = fragments.Max(fragment => fragment.Left + fragment.Width);
                    box = box with
                    {
                        X = origin.X + left * singleLine.Scale,
                        Width = (edge - left) * singleLine.Scale
                    };
                }
            }
            if (visual?.StyledSource is not null)
                box = CaptureStyledTextElement(visual, line, displayStart, displayEnd, origin, box);
            for (var scalar = first; scalar < last; scalar++)
            {
                characters[scalar] = box;
                carets[scalar] = caret;
            }
        }
        carets[^1] = Caret(text.Length);
        return new(content, key, origin, clip, height, visual?.Version ?? singleLine?.Version ?? 0, characters, carets, lineRanges)
        {
            Composition = composition,
            VisualKey = visualKey
        };
    }

    private UiRect CaptureStyledTextElement(UiVisualTextLines visual, int line, int start, int end,
        UiVector2 origin, UiRect bounds)
    {
        var projection = visual.StyledSource!.Projection;
        var first = projection.SourceToDisplay[start];
        var last = projection.SourceToDisplay[end];
        if (first == last)
            return bounds with
            {
                Width = 0
            };
        var displayed = projection.Text.AsSpan(first, last - first);
        if (displayed.Contains('\r') || displayed.Contains('\n'))
            return bounds with
            {
                Width = 0
            };
        if (visual.StyledTableRows.ContainsKey(line))
        {
            var fragments = visual.SelectionFragments(start, end, line);
            if (fragments.Length == 0)
                return bounds with
                {
                    Width = 0
                };
            var left = fragments.Min(fragment => fragment.Left);
            var right = fragments.Max(fragment => fragment.Left + fragment.Width);
            bounds = bounds with
            {
                X = origin.X + left,
                Width = right - left
            };
        }
        var style = projection.StyleAt(first);
        using var typography = BeginStyledTypography(style);
        var height = typography.LineHeight;
        if (IsStyledImage(displayed, style))
        {
            if (visual.TryComposedObjectBounds(start, line, out var objectBounds))
                return objectBounds with
                {
                    X = origin.X + objectBounds.X,
                    Y = origin.Y + objectBounds.Y
                };
            height = StyledImageSize(projection.Images[style.ImageIndex - 1], visual.EffectiveWidth).Y;
        }
        else if (IsStyledTask(displayed, style))
            height = bounds.Width;
        return bounds with
        {
            Y = origin.Y + visual.Y(line) + (visual.LineHeight(line) - height) / 2,
            Height = height
        };
    }
}
