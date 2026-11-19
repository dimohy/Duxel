namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const int RetainedInputSlotDepth = 8;
    private UiDrawListBuilder?[]? _inputSlotBuilders;
    private int _inputSlotDepth;
    private UiRect? _inputSlotBounds;
    private int _inputSlotBoundsTier;
    private bool _layoutChildConstraints;
    internal (int Builders, int Depth) InputSlotCaptureStorage => (_inputSlotBuilders?.Count(static b => b is not null) ?? 0, _inputSlotDepth);
    private UiDrawListBuilder BeginInputSlotCapture()
    {
        var depth = _inputSlotDepth++;
        UiDrawListBuilder capture;
        if (depth < RetainedInputSlotDepth)
        {
            _inputSlotBuilders ??= new UiDrawListBuilder?[RetainedInputSlotDepth];
            capture = _inputSlotBuilders[depth] ??= new(CurrentClipRect, false);
        }
        else
            capture = new(CurrentClipRect, false);
        capture._SetClipRect(CurrentClipRect);
        capture._ResetForNewFrame();
        capture.SetLayerScene3D(_builder.LayerScene3D);
        capture._SetDrawListSharedData(GetDrawListSharedData());
        capture.PushTexture(_whiteTexture);
        return capture;
    }
    private void EndInputSlotCapture(UiDrawListBuilder capture, bool completed)
    {
        try
        {
            if (!completed)
                capture.DiscardDeferred();
        }
        finally
        {
            if (--_inputSlotDepth >= RetainedInputSlotDepth)
                capture._ClearFreeMemory();
        }
    }
    private bool RenderAllocatedChild(UiDrawListBuilder capture, UiWidget child, UiRect bounds, UiRect root, bool constrainLayout = false,
        bool clipToAllocation = true, bool renderContentOnly = false)
    {
        var previous = _builder;
        var previousBounds = _inputSlotBounds;
        var previousTier = _inputSlotBoundsTier;
        var previousConstraints = _layoutChildConstraints;
        var routing = (_columnsActive, _tableActive);
        var item = (_lastItemPos, _lastItemSize, _lastItemId, _lastItemFlags, _lastItemEdited, _lastItemToggledOpen, _lastItemToggledSelection, _lastItemSelectionUserData, _hasLastItemSelectionUserData);
        var contentItem = item;
        var next = (_nextItemWidth, _nextItemVerticalAlign, _hasNextItemVerticalAlign);
        var layoutDepth = _layouts.Count;
        var tabStart = _currentTabStops.Count;
        _builder = capture;
        _inputSlotBounds = bounds;
        _inputSlotBoundsTier = _popupTierDepth;
        _layoutChildConstraints = constrainLayout;
        _layouts.Push(new(new(bounds.X, bounds.Y), false, 0, bounds.X));
        var completed = false;
        try
        {
            var inheritedClip = ResolveItemClipRect();
            // These bounds already include parent placement; legacy routing must not move them again.
            _columnsActive = false;
            _tableActive = false;
            var clipAllocation = clipToAllocation && !child.AllowsVisualOverflow;
            using var clip = BeginControlClip(IntersectRect(inheritedClip, clipAllocation ? IntersectRect(root, bounds) : root));
            if (renderContentOnly)
                child.RenderUnpresentedContent(this);
            else
                child.Render(this);
            if (renderContentOnly)
                contentItem = (_lastItemPos, _lastItemSize, _lastItemId, _lastItemFlags, _lastItemEdited, _lastItemToggledOpen, _lastItemToggledSelection, _lastItemSelectionUserData, _hasLastItemSelectionUserData);
            completed = true;
            for (var i = tabStart; i < _currentTabStops.Count; i++)
            {
                if (_currentTabStops[i].Id == _state.FocusedId)
                    return true;
            }
            return false;
        }
        finally
        {
            var balanced = _layouts.Count == layoutDepth + 1;
            while (_layouts.Count > layoutDepth)
                _layouts.Pop();
            _builder = previous;
            _inputSlotBounds = previousBounds;
            _inputSlotBoundsTier = previousTier;
            _layoutChildConstraints = previousConstraints;
            (_columnsActive, _tableActive) = routing;
            (_lastItemPos, _lastItemSize, _lastItemId, _lastItemFlags, _lastItemEdited, _lastItemToggledOpen, _lastItemToggledSelection, _lastItemSelectionUserData, _hasLastItemSelectionUserData) = item;
            if (completed && renderContentOnly)
            {
                (_lastItemPos, _lastItemSize, _lastItemId, _lastItemFlags, _lastItemEdited, _lastItemToggledOpen, _lastItemToggledSelection, _lastItemSelectionUserData, _hasLastItemSelectionUserData) = contentItem;
                _lastItemPos = item.Item1;
                _lastItemSize = item.Item2;
            }
            (_nextItemWidth, _nextItemVerticalAlign, _hasNextItemVerticalAlign) = next;
            if (completed && !balanced)
                throw new InvalidOperationException("Child left an unbalanced layout scope.");
        }
    }
    private void ReleaseInputSlotResources()
    {
        _widgetImeHandlers.Clear();
        if (_inputSlotBuilders is null)
            return;
        foreach (var builder in _inputSlotBuilders)
            builder?._ClearFreeMemory();
        _inputSlotBuilders = null;
        _inputSlotBounds = null;
        _inputSlotDepth = 0;
    }
}
