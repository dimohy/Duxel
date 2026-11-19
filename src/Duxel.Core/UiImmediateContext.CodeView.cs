namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private (string Id, UiAccessibilityTextLayout Layout, int Frame)? _clippedCodeTextLayout;

    private float CodeGutter(UiCodeView code) => code.Options.LineNumbers
        ? MeasureTextInternal(code.LineStarts.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), _textSettings, _lineHeight).X + 24
        : 8;
    internal UiVector2 MeasureCodeView(UiCodeView code)
    {
        var context = new UiControlContext(this);
        using var type = context.BeginTypography<UiMonospaceTypeface, UiFluentType300>();
        var constraints = context.LayoutConstraints;
        var width = constraints.MaxWidth;
        if (!float.IsFinite(width)) throw new InvalidOperationException("Code view needs a finite parent width.");
        var height = code.LineStarts.Length * type.LineHeight;
        if (code.Options.Wrap)
        {
            var contentWidth = MathF.Max(1, width - CodeGutter(code) - 16);
            var measurement = code.Measurements.ForWidth(contentWidth);
            measurement.Prepare(this, code.Code, contentWidth,
                float.MaxValue, type.LineHeight, 0, true, styled : code.Document);
            height = measurement.Height;
        }
        if (!code.Expanded && code.Options.MaximumVisibleLines is { } maximum)
            height = MathF.Min(height, maximum * type.LineHeight);
        return constraints.Constrain(new(width, height + 24 + ScrollbarSize));
    }
    internal void RenderCodeView(UiCodeView code, string id)
    {
        var context = new UiControlContext(this);
        var constraints = context.LayoutConstraints;
        // The parent already measured this surface. Prepare text below only when it is visible.
        var size = constraints.MinWidth == constraints.MaxWidth && constraints.MinHeight == constraints.MaxHeight
            ? new UiVector2(constraints.MaxWidth, constraints.MaxHeight)
            : MeasureCodeView(code);
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var root = ResolveId(id);
        var text = code.Code;
        var clipBounds = IntersectRect(CurrentClipRect, bounds);
        if (clipBounds.Width <= 0 || clipBounds.Height <= 0)
        {
            // Keep at most one small geometry snapshot while its widget is still rendered.
            // The offscreen node publishes no text geometry.
            if (_accessibility is { } tree)
            {
                var frame = _state.FrameCount;
                if (tree.TryGet(root, out var previous) && previous.Node.TextLayout is { CanRetainClippedGeometry: true } geometry)
                    _clippedCodeTextLayout = (root, geometry, frame);
                else if (_clippedCodeTextLayout is { } clipped && clipped.Id == root)
                    _clippedCodeTextLayout = (root, clipped.Layout, frame);
            }
            ExposeAccessibility(root, _accessibilityParent, UiAccessibilityRole.Edit, "C# 예제 소스", bounds,
                text, readOnly : true, multiline : true, textInfo : CaptureAccessibilityText(root, text, 0, 0));
            FinishLayoutBox(origin, size);
            return;
        }
        using var type = context.BeginTypography<UiMonospaceTypeface, UiFluentType300>();
        var gutter = CodeGutter(code);
        code.Owner.Bind(this, root);
        code.Owner.UsePreparedLines(code.Options.Wrap && (code.Expanded || code.Options.MaximumVisibleLines is null)
            ? code.Measurements.ForWidth(MathF.Max(1, bounds.Width - gutter - 16)) : code.IndependentPaintLines);
        var layout = new UiMultilineInputLayout(bounds, type.LineHeight)
        {
            Content = new(bounds.X + gutter + 4, bounds.Y + 12, MathF.Max(0, bounds.Width - gutter - 16), MathF.Max(0, bounds.Height - 24)),
            Options = new() { Flags = UiInputTextFlags.ReadOnly, Wrap = code.Options.Wrap },
            Wrap = code.Options.Wrap, InputOwner = code.Owner, VisualLines = code.Owner.Lines,
            StyledSource = code.Document, StyledEditorId = root
        };
        var parent = _accessibilityParent;
        using var clip = context.Clip(bounds);
        try
        {
            ClaimAccessibilityField(root);
            ClaimFieldFocus(root, bounds);
            _accessibilityParent = root;
            var edit = context.EditNavigableMultiline(root, ref text, 1_048_576, layout);
            edit = ApplyMultilineAccessibilityScroll(root, layout, edit);
            var area = edit.VisualContent!.Value;
            if (_mouseWheel != 0 && IsHovering(clipBounds) && edit.ContentHeight - area.Height <= .5f
                && _widgetTransformDepth == _widgetScreenTransformDepth)
                _state.DocumentWheelRegion = (root, bounds);
            // Text that fits its field has no internal scroll range to advance during a drag.
            // Keep ordered moves across the code clip when the text has its own scroll range.
            if (_state.OwnsTextDrag(root) && _widgetTransformDepth == _widgetScreenTransformDepth
                && _state.PreviousPopupBlockingRects.Count == 0
                && IntersectRect(clipBounds, area) is { Width: > 0, Height: > 0 } selectionClip)
            {
                var textFits = edit.ContentHeight - area.Height <= .5f && edit.ContentWidth - area.Width <= .5f;
                var motionBounds = textFits ? new UiRect(0, 0, _displaySize.X, _displaySize.Y) : selectionClip;
                _state.PointerMoveCoalescing = (root, motionBounds);
            }
            if (IsPresentationPass)
            {
                var palette = UiFluentInputPalette.Resolve(context, UiButtonVisualState.Rest, UiInputAppearance.Outline, false);
                context.Frame(bounds, palette.Background, context.Theme.ForcedColors?.CanvasText ?? context.Theme.Separator, 6, 1);
                PaintMultilineInput(layout, edit, new(palette.Text, context.Theme.InputSelectionBg, palette.Text) { Typography = true });
                if (code.Options.LineNumbers)
                    PaintCodeLineNumbers(code, context, gutter, bounds, clipBounds, area, edit.ScrollY);
            }
            else if (edit.AccessibilityText is { } info)
            {
                var palette = UiFluentInputPalette.Resolve(context, UiButtonVisualState.Rest, UiInputAppearance.Outline, false);
                CaptureStyledTextPaint(root, info, code.Owner.Lines, palette.Text);
            }
            CompleteMultilineInput(root, layout, edit, code.Owner.Scrollbars);
            var scroll = new UiAccessibilityScrollInfo(new(area.Width, area.Height), new(edit.ContentWidth, edit.ContentHeight), new(edit.ScrollX, edit.ScrollY));
            ExposeAccessibility(root, parent, UiAccessibilityRole.Edit, "C# 예제 소스", bounds, text, readOnly : true,
                multiline : true, scroll : CanPublishScroll(IntersectRect(CurrentClipRect, area), scroll) ? scroll : null,
                textInfo : edit.AccessibilityText, textLayout : CaptureMultilineTextLayout(root, edit.AccessibilityText, layout, edit,
                    reusePositionIndependentGeometry: true));
            _accessibility?.CaptureScrollViewport(root, area, CurrentClipRect, scroll);
        }
        finally { _accessibilityParent = parent; FinishLayoutBox(origin, size); }
    }

    private void PaintCodeLineNumbers(UiCodeView code, UiControlContext context, float gutter, UiRect bounds,
        UiRect clip, UiRect content, float scroll)
    {
        context.Frame(new(bounds.X + gutter - 4, bounds.Y + 8, 1, MathF.Max(0, bounds.Height - 16)),
            context.Theme.Separator, default, 0, 0);
        var visual = code.Owner.Lines;
        for (var line = visual.LineAtY(MathF.Max(0, clip.Y - content.Y + scroll)); line < visual.Count; line++)
        {
            var y = content.Y + visual.Y(line) - scroll;
            if (y >= clip.Y + clip.Height)
                break;
            var number = Array.BinarySearch(code.LineStarts, visual[line].Start);
            if (number < 0)
                continue;
            var label = (number + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var measured = context.MeasureText(label);
            PaintInputRun(label, new(bounds.X + gutter - 12 - measured.X, y),
                context.Theme.ForcedColors?.CanvasText ?? context.Theme.TextDisabled, clip, true);
        }
    }
}
