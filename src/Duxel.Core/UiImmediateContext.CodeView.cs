namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const int ClippedCodeLayoutCapacity = 4;
    private readonly record struct ClippedCodeLayout(string Id, UiAccessibilityTextLayout Layout, int Frame);
    private List<ClippedCodeLayout>? _clippedCodeLayouts;

    private void RetainClippedCodeLayout(string id, UiAccessibilityTextLayout? geometry)
    {
        var cache = _clippedCodeLayouts;
        var index = FindClippedCodeLayout(id);
        var frame = _state.FrameCount;
        if (geometry is { CanRetainClippedGeometry: true })
        {
            cache ??= _clippedCodeLayouts = new(ClippedCodeLayoutCapacity);
            // Keep the most recent snapshots of code that has just left the viewport.
            // Each entry must also meet the geometry byte limit.
            if (index >= 0)
                cache.RemoveAt(index);
            else if (cache.Count == ClippedCodeLayoutCapacity)
                cache.RemoveAt(0);
            cache.Add(new(id, geometry, frame));
        }
        else if (index >= 0)
            cache![index] = cache[index] with { Frame = frame };
    }

    private int FindClippedCodeLayout(string id)
    {
        var cache = _clippedCodeLayouts;
        if (cache is not null)
            for (var i = cache.Count - 1; i >= 0; i--)
                if (cache[i].Id == id)
                    return i;
        return -1;
    }

    private UiAccessibilityTextLayout? TakeClippedCodeLayout(string id)
    {
        var index = FindClippedCodeLayout(id);
        if (index < 0)
            return null;
        var cache = _clippedCodeLayouts!;
        var layout = cache[index].Layout;
        cache.RemoveAt(index);
        return layout;
    }

    private void UpdateClippedCodeLayouts(bool sameTree)
    {
        if (_clippedCodeLayouts is not { Count: > 0 } cache)
            return;
        if (!sameTree)
        {
            cache.Clear();
            return;
        }
        for (var i = cache.Count - 1; i >= 0; i--)
            if (unchecked(_state.FrameCount - cache[i].Frame) > 1)
                cache.RemoveAt(i);
    }

    private float CodeGutter(UiCodeView code) => code.Options.LineNumbers
        ? MeasureTextInternal(code.GetLineNumberText(code.LineStarts.Length), _textSettings, _lineHeight).X + 24
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
            if (!measurement.TryMeasureCodeHeight(this, code.Document, code.Code, contentWidth, type.LineHeight, out height))
            {
                measurement.Prepare(this, code.Code, contentWidth,
                    float.MaxValue, type.LineHeight, 0, true, styled : code.Document);
                height = measurement.Height;
            }
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
            if (code.SelectionMenu is { } popup)
            {
                CancelMenuPopup(popup.Input);
                popup.Source = null;
            }
            if (_leftMousePressed && _state.ActiveId == root)
                _state.ActiveId = null;
            // Keep a bounded geometry snapshot while its widget is still rendered.
            // The offscreen node publishes no text geometry.
            if (_accessibility is { } tree)
                RetainClippedCodeLayout(root, tree.TryGet(root, out var previous) ? previous.Node.TextLayout : null);
            ExposeAccessibility(root, _accessibilityParent, UiAccessibilityRole.Edit, code.Labels.AccessibleName, bounds,
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
            if (code.SelectionMenu is { Input.Open: true } popup
                && (!ReferenceEquals(popup.Source, code.Code)
                    || popup.Selection.HasSelection && !edit.Selection.HasSelection))
            {
                CancelMenuPopup(popup.Input, root);
                popup.Source = null;
            }
            var area = edit.VisualContent!.Value;
            if (_mouseWheel != 0 && IsHovering(clipBounds) && edit.ContentHeight - area.Height <= .5f
                && _widgetTransformDepth == _widgetScreenTransformDepth)
                _state.DocumentWheelRegion = (root, bounds);
            // A drag outside this clip can scroll the editor or its enclosing document.
            // Coalesce motion only inside the visible text area.
            if (_state.OwnsTextDrag(root) && _widgetTransformDepth == _widgetScreenTransformDepth
                && _state.PreviousPopupBlockingRects.Count == 0
                && IntersectRect(clipBounds, area) is { Width: > 0, Height: > 0 } selectionClip)
            {
                _state.PointerMoveCoalescing = (root, selectionClip);
            }
            if (IsPresentationPass)
            {
                var palette = UiFluentInputPalette.Resolve(context, UiButtonVisualState.Rest, UiInputAppearance.Outline, false);
                context.Frame(bounds, palette.Background, context.Theme.ForcedColors?.CanvasText ?? context.Theme.Separator, 6, 1);
                PaintMultilineInput(layout, edit, new(palette.Text, context.Theme.InputSelectionBg, palette.Text) { Typography = true },
                    keepSelectionVisible: code.SelectionMenu is { Input.Open: true, Selection.HasSelection: true });
                if (code.Options.LineNumbers)
                {
                    _retainCodeLineNumbers = true;
                    try { PaintCodeLineNumbers(code, context, gutter, bounds, clipBounds, area, edit.ScrollY); }
                    finally { _retainCodeLineNumbers = false; }
                }
            }
            else if (edit.AccessibilityText is { } info)
            {
                var palette = UiFluentInputPalette.Resolve(context, UiButtonVisualState.Rest, UiInputAppearance.Outline, false);
                CaptureStyledTextPaint(root, info, code.Owner.Lines, palette.Text);
            }
            CompleteMultilineInput(root, layout, edit, code.Owner.Scrollbars);
            var scroll = new UiAccessibilityScrollInfo(new(area.Width, area.Height), new(edit.ContentWidth, edit.ContentHeight), new(edit.ScrollX, edit.ScrollY));
            ExposeAccessibility(root, parent, UiAccessibilityRole.Edit, code.Labels.AccessibleName, bounds, text, readOnly : true,
                multiline : true, scroll : CanPublishScroll(IntersectRect(CurrentClipRect, area), scroll) ? scroll : null,
                textInfo : edit.AccessibilityText, textLayout : CaptureMultilineTextLayout(root, edit.AccessibilityText, layout, edit,
                    reusePositionIndependentGeometry: true));
            _accessibility?.CaptureScrollViewport(root, area, CurrentClipRect, scroll);
            RenderCodeSelectionMenu(code, root, clipBounds, edit);
        }
        finally { _accessibilityParent = parent; FinishLayoutBox(origin, size); }
    }

    private void RenderCodeSelectionMenu(UiCodeView code, string root, UiRect clip, in UiMultilineEditResult edit)
    {
        var popup = code.SelectionMenu;
        var open = _rightMousePressed && IsControlEnabled && !_layoutPointerBlocked && IsHovering(clip)
            && _popupTierDepth >= _blockingKeyboardTier && !IsMouseOverAnyOpenMenuPopup()
            && !IsMouseOverHigherTierPopup(_popupTierDepth);
        if (open)
        {
            popup ??= code.SelectionMenu = new(code.Labels);
            popup.Anchor = new(_mousePosition.X, _mousePosition.Y, 1, 1);
            popup.Source = code.Code;
            popup.Selection = edit.Active ? edit.Selection : default;
        }
        else if (popup is not { Input.Open: true })
            return;

        var menu = popup.Input;
        var items = popup.Selection.HasSelection ? popup.CopyItems : popup.SelectAllItems;
        menu.Prepare(ResolveCachedIdentifier(root, "##selection-menu", false), items);
        menu.TriggerId = root;
        menu.FocusOwnerId = root;
        var widget = _accessibilityWidget;
        _accessibilityWidget = null;
        try
        {
            var invoked = RenderMenuPopup<UiFluentMenuPresentation>(menu.Root!, code.Labels.ContextMenuName, items,
                UiFluentMenuPresentation.DefaultOptions, menu, popup.Anchor, default, controlledOpen: open ? true : null);
            if (invoked == "copy")
            {
                var (start, end) = GetSelectionRange(popup.Selection);
                SetClipboardText(code.Code[start..end]);
            }
            else if (invoked == "select-all")
            {
                code.Owner.Lines.ClearAffinity();
                _state.SetSelection(root, new(0, code.Code.Length));
                _state.SetCursor(root, code.Code.Length);
                _state.ActiveId = root;
                ResetCaretBlink();
            }
        }
        finally
        {
            if (!menu.Open)
                popup.Source = null;
            _accessibilityWidget = widget;
        }
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
            var label = code.GetLineNumberText(number + 1);
            var measured = context.MeasureText(label);
            PaintInputRun(label, new(bounds.X + gutter - 12 - measured.X, y),
                context.Theme.ForcedColors?.CanvasText ?? context.Theme.TextDisabled, clip, true);
        }
    }
}
