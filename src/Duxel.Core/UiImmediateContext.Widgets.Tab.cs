namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private readonly record struct UiTabBarLayoutState(string? PreviousId, string? PreviousActiveKey,
        UiVector2 ContentOrigin, float ContentBottom, int HeaderDepth, string? ActiveLabel = null, bool InItem = false)
    {
        internal string AccessibilityParent { get; init; } = string.Empty;
        internal UiRect HeaderBounds { get; init; }
        internal UiRect PanelClip { get; init; }
        internal string? ActiveTabId { get; init; }
        internal int HeaderCount { get; init; }
        internal bool SelectionRequested { get; init; }
    }

    public bool BeginTabBar(string id)
    {
        var previousId = _currentTabBarId;
        var previousActiveKey = _currentTabBarActiveKey;
        _currentTabBarId = ResolveId(id ?? "TabBar");
        _currentTabBarActiveKey = $"{_currentTabBarId}##active";

        var frameHeight = GetFrameHeight() + 2f;
        var width = _hasWindowRect ? MathF.Max(0f, _windowRect.Width - (WindowPadding * 2f)) : InputWidth;
        var cursor = _layouts.Peek().Cursor;
        var rect = new UiRect(cursor.X, cursor.Y, width, frameHeight);
        AddRectFilled(rect, _theme.MenuBarBg, _whiteTexture);

        BeginRow();
        var content = new UiVector2(cursor.X, cursor.Y + frameHeight + RowSpacing);
        _tabBarLayouts.Push(new(previousId, previousActiveKey, content, content.Y, _layouts.Count)
        {
            AccessibilityParent = _accessibilityParent,
            HeaderBounds = rect
        });
        return true;
    }

    public bool BeginTabBar(string id, UiTabBarFlags flags)
    {
        _ = flags;
        return BeginTabBar(id);
    }

    public bool BeginTabItem(string label)
    {
        label ??= "Tab";
        if (_currentTabBarId is null || _currentTabBarActiveKey is null)
        {
            return false;
        }

        var bar = _tabBarLayouts.Pop();
        if (bar.InItem)
            throw new InvalidOperationException("EndTabItem must precede the next tab header.");
        var activeLabel = bar.ActiveLabel ?? _state.GetTextBuffer(_currentTabBarActiveKey, label);
        if (activeLabel.Length == 0)
        {
            activeLabel = label;
            _state.SetTextBuffer(_currentTabBarActiveKey, label);
        }
        bar = bar with { ActiveLabel = activeLabel };
        var isActive = string.Equals(activeLabel, label, StringComparison.Ordinal);

        var textSize = MeasureTextInternal(label, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X + (ButtonPaddingX * 2f), GetFrameHeight() + 2f);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var headerKey = $"{_currentTabBarId}##{label}";
        var headerId = ResolveId(headerKey);
        var pressed = ButtonBehavior(headerKey, rect, out var hovered, out var held);
        if (_accessibility is { } tree)
        {
            var clip = IntersectRect(CurrentClipRect, rect);
            if (!bar.SelectionRequested && tree.Take(headerId, UiAccessibilityAction.Select) is { } selection)
            {
                selection.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled
                    : clip.Width <= 0 || clip.Height <= 0 || _popupTierDepth < _blockingKeyboardTier
                        ? UiAccessibilityResult.Unavailable : UiAccessibilityResult.Success;
                if (selection.Result == UiAccessibilityResult.Success)
                {
                    pressed = true;
                    bar = bar with { SelectionRequested = true };
                    if (!isActive) tree.AwaitSelectedPublication(selection);
                }
            }
            if (tree.Take(headerId, UiAccessibilityAction.Deselect) is { } deselection)
            {
                deselection.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled
                    : clip.Width <= 0 || clip.Height <= 0 || _popupTierDepth < _blockingKeyboardTier
                        ? UiAccessibilityResult.Unavailable
                        : isActive ? UiAccessibilityResult.InvalidOperation : UiAccessibilityResult.Success;
            }
            tree.Add(new(headerId, _currentTabBarId, UiAccessibilityRole.Tab, GetDisplayLabel(label), clip,
                IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, clip.Width <= 0 || clip.Height <= 0,
                false, Focusable : false, ValuePattern : false) { Selected = isActive }, rect);
        }
        bar = bar with
        {
            HeaderCount = bar.HeaderCount + 1,
            HeaderBounds = bar.HeaderBounds with { Width = MathF.Max(bar.HeaderBounds.Width, rect.X + rect.Width - bar.HeaderBounds.X) }
        };
        if (pressed)
        {
            _state.SetTextBuffer(_currentTabBarActiveKey, label);
            // Selection is applied next frame so only one tab body can render in this frame.
            if (!isActive)
                RequestFrameAt(_state.TimeSeconds);
        }

        var bg = isActive ? _theme.TabActive : held ? _theme.ButtonActive : hovered ? _theme.TabHovered : _theme.Tab;
        AddRectFilled(rect, bg, _whiteTexture);

        var textPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (rect.Height - textSize.Y) * 0.5f);
        AddTextInternal(_builder,

            label,
            textPos,
            _theme.TabText,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        if (isActive)
        {
            _layouts.Push(new UiLayoutState(bar.ContentOrigin, false, 0, bar.ContentOrigin.X));
            bar = bar with { InItem = true };
            if (_accessibility is not null)
            {
                bar = bar with { ActiveTabId = headerId, PanelClip = ResolveLabeledRegionClip(bar.ContentOrigin) };
                _accessibilityParent = LegacyTabPanelId(headerId);
            }
        }
        _tabBarLayouts.Push(bar);
        return isActive;
    }

    public bool BeginTabItem(string label, UiTabItemFlags flags)
    {
        _ = flags;
        return BeginTabItem(label);
    }

    public void SetTabItemClosed(string label)
    {
        if (_currentTabBarActiveKey is null || string.IsNullOrEmpty(label))
        {
            return;
        }

        var activeLabel = _state.GetTextBuffer(_currentTabBarActiveKey, string.Empty);
        if (string.Equals(activeLabel, label, StringComparison.Ordinal))
        {
            _state.SetTextBuffer(_currentTabBarActiveKey, string.Empty);
            RequestFrameAt(_state.TimeSeconds);
        }
    }

    public bool TabItemButton(string label)
    {
        label ??= "Tab";

        var textSize = MeasureTextInternal(label, _textSettings, _lineHeight);
        var size = new UiVector2(textSize.X + (ButtonPaddingX * 2f), GetFrameHeight() + 2f);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var buttonKey = $"{_currentTabBarId ?? "tab"}##btn{label}";
        var buttonId = ResolveId(buttonKey);
        var pressed = ButtonBehavior(buttonKey, rect, out var hovered, out var held);
        pressed |= ApplyAccessibilityInvoke(buttonId, rect, default).Activated;
        ExposeAccessibility(buttonId, _accessibilityParent, UiAccessibilityRole.Button, label, rect,
            focusable : false, valuePattern : false);
        var bg = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        AddRectFilled(rect, bg, _whiteTexture);

        var textPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (rect.Height - textSize.Y) * 0.5f);
        AddTextInternal(_builder,

            label,
            textPos,
            _theme.TabText,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        return pressed;
    }

    public void EndTabItem()
    {
        if (!_tabBarLayouts.TryPop(out var bar) || !bar.InItem || _layouts.Count != bar.HeaderDepth + 1)
            throw new InvalidOperationException("Tab item layout scopes are unbalanced.");
        var content = _layouts.Pop();
        var bottom = MathF.Max(content.Cursor.Y, _lastItemPos.Y + _lastItemSize.Y);
        _accessibilityParent = bar.AccessibilityParent;
        if (_accessibility is { } tree)
        {
            var bounds = new UiRect(bar.ContentOrigin.X, bar.ContentOrigin.Y,
                MathF.Max(0, bar.PanelClip.X + bar.PanelClip.Width - bar.ContentOrigin.X), MathF.Max(0, bottom - bar.ContentOrigin.Y));
            var clip = IntersectRect(bar.PanelClip, bounds);
            var panelId = LegacyTabPanelId(bar.ActiveTabId!);
            tree.Add(new(panelId, bar.AccessibilityParent, UiAccessibilityRole.TabPanel, GetDisplayLabel(bar.ActiveLabel!), clip,
                IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, clip.Width <= 0 || clip.Height <= 0,
                false, Focusable : false, ValuePattern : false) { LabeledBy = bar.ActiveTabId }, bounds);
            tree.ConnectTabPanel(bar.ActiveTabId!, panelId);
        }
        _tabBarLayouts.Push(bar with { InItem = false, ContentBottom = MathF.Max(bar.ContentBottom, bottom) });
    }

    public void EndTabBar()
    {
        if (!_tabBarLayouts.TryPop(out var bar) || bar.InItem || _layouts.Count != bar.HeaderDepth)
            throw new InvalidOperationException("Tab bar layout scopes are unbalanced.");
        if (_accessibility is { } tree)
        {
            var clip = IntersectRect(CurrentClipRect, bar.HeaderBounds);
            tree.Add(new(_currentTabBarId!, bar.AccessibilityParent, UiAccessibilityRole.TabList, GetDisplayLabel(_currentTabBarId!), clip,
                IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier, clip.Width <= 0 || clip.Height <= 0,
                false, Focusable : false, ValuePattern : false) { SelectionRequired = bar.HeaderCount > 0 }, bar.HeaderBounds);
        }
        EndRow();
        var parent = _layouts.Pop();
        _layouts.Push(parent with { Cursor = new(parent.Cursor.X, MathF.Max(parent.Cursor.Y, bar.ContentBottom + RowSpacing)) });
        _currentTabBarId = bar.PreviousId;
        _currentTabBarActiveKey = bar.PreviousActiveKey;
    }

    private static string LegacyTabPanelId(string headerId) => headerId + "\u001fpanel";
}

