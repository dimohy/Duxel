namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderTabPanel(string id, string tabId, string label, UiWidget? content,
        Action<UiImmediateContext>? render, bool focusable)
        => RenderLabeledRegion(id, tabId, label, content, render, focusable, UiAccessibilityRole.TabPanel);

    internal void RenderNavigationPage(string id, string? destinationId, string label,
        Action<UiImmediateContext> render, bool focusable)
        => RenderLabeledRegion(id, destinationId, label, null, render, focusable, UiAccessibilityRole.Pane);

    private void RenderLabeledRegion(string id, string? labelId, string label, UiWidget? content,
        Action<UiImmediateContext>? render, bool focusable, UiAccessibilityRole role)
    {
        EnsureNotMeasuring();
        var start = GetCursorScreenPos();
        var scopeClip = ResolveLabeledRegionClip(start);
        var width = MathF.Max(0, scopeClip.X + scopeClip.Width - start.X);
        var previousItem = (_lastItemPos, _lastItemSize, _lastItemId);
        var previousParent = _accessibilityParent;
        var tabStart = _currentTabStops.Count;
        _accessibilityParent = id;
        try
        {
            if (content is not null) content.Render(this);
            else render!(this);
        }
        finally { _accessibilityParent = previousParent; }
        var layout = _layouts.Peek();
        var hasContent = layout.Cursor != start || previousItem != (_lastItemPos, _lastItemSize, _lastItemId);
        var bottom = MathF.Max(layout.Cursor.Y + (layout.IsRow ? layout.RowMaxHeight : 0),
            _lastItemPos.Y + _lastItemSize.Y);
        var height = hasContent ? MathF.Max(0, bottom - start.Y) : 0;
        var bounds = new UiRect(start.X, start.Y, width, height);
        var clip = IntersectRect(scopeClip, bounds);
        var focused = false;
        if (focusable)
        {
            var last = _lastItemId;
            _lastItemId = id;
            UiButtonInteraction interaction;
            try { interaction = ProcessControlKeyboard<RangeControlKeys>(bounds, default); }
            finally { _lastItemId = last; }
            if (_currentTabStops.Count > tabStart && _currentTabStops[^1].Id == id)
            {
                var stop = _currentTabStops[^1];
                _currentTabStops.RemoveAt(_currentTabStops.Count - 1);
                _currentTabStops.Insert(tabStart, stop);
            }
            focused = interaction.Focused;
            // A panel can fill (or exceed) its viewport. Keep all four edges inside its visible bounds.
            if (interaction.FocusVisible && clip.Width > 0 && clip.Height > 0)
                AddDesignedFrame(clip, new UiColor(0x00FFFFFF), _theme.InputBorderActive, 0,
                    _designTokens.FocusRingThickness);
        }
        if (_accessibility is { } tree)
        {
            tree.Add(new(id, previousParent, role, label, clip,
                IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier,
                clip.Width <= 0 || clip.Height <= 0, focused, Focusable : focusable, ValuePattern : false)
                { LabeledBy = labelId }, bounds);
            if (role == UiAccessibilityRole.TabPanel) tree.ConnectTabPanel(labelId!, id);
            else if (labelId is not null) tree.ConnectNavigationPage(labelId, id);
        }
    }

    private UiRect ResolveLabeledRegionClip(UiVector2 start)
    {
        var clip = ResolveItemClipRect();
        if (!_hasWindowRect && !HasCurrentChild && _inputSlotBounds is null)
            return clip;
        // A scrolled cursor is already in screen coordinates. Its remaining layout
        // budget must not be used as the height of a screen-space clipping rectangle.
        var end = GetContentRegionMax();
        var columnEnd = _columnsActive && _columnsCount > 0 && !HasCurrentChild && !HasCurrentFlowRegion
            && !(_inputSlotBounds is not null && _inputSlotBoundsTier == _popupTierDepth)
            ? GetColumnsColumnX(_columnsIndex) + GetColumnsColumnWidth(_columnsIndex) : end.X;
        return IntersectRect(clip, new(start.X, start.Y,
            MathF.Max(0, columnEnd - start.X), MathF.Max(0, end.Y - start.Y)));
    }
}
