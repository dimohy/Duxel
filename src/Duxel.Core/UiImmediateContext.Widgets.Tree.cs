namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public void SetNextItemOpen(bool isOpen)
    {
        _nextItemOpen = isOpen;
        _hasNextItemOpen = true;
    }

    public void SetNextItemStorageID(string id)
    {
        id ??= string.Empty;
        _nextItemStorageId = ResolveId(id);
    }

    public bool CollapsingHeader(string label, bool defaultOpen = false)
    {
        label ??= "Header";
        var rawId = $"{label}##collapse";
        var id = _nextItemStorageId ?? ResolveId(rawId);
        _nextItemStorageId = null;

        var isOpen = _state.GetBool(id, defaultOpen);
        if (_hasNextItemOpen)
        {
            isOpen = _nextItemOpen;
            _state.SetBool(id, isOpen);
            _hasNextItemOpen = false;
        }
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var arrowSize = MathF.Min(height, 12f);
        var available = GetContentRegionAvail();
        var minWidth = textSize.X + (ButtonPaddingX * 2f) + arrowSize + 6f;
        var width = MathF.Max(minWidth, available.X);
        var size = new UiVector2(width, height);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior(rawId, rect, out var hovered, out var held);
        if (pressed)
        {
            isOpen = !isOpen;
            _state.SetBool(id, isOpen);
        }
        _lastItemToggledOpen = pressed;

        var headerBlend = AnimateFloat(
            $"{id}##header_blend",
            held ? 1f : hovered ? 0.65f : 0f,
            durationSeconds: 0.12f,
            easing: UiAnimationEasing.OutCubic
        );
        var bgBase = ApplyAlpha(_theme.Header, 0.90f);
        AddRectFilled(rect, bgBase, _whiteTexture);
        if (headerBlend > 0.001f)
        {
            var accent = held ? _theme.HeaderActive : _theme.HeaderHovered;
            AddRectFilled(rect, ApplyAlpha(accent, headerBlend), _whiteTexture);
        }

        var arrowRect = new UiRect(rect.X + ButtonPaddingX, rect.Y + (height - arrowSize) * 0.5f, arrowSize, arrowSize);
        DrawTreeArrow(arrowRect, isOpen, id, _theme.Text);

        var textPos = new UiVector2(arrowRect.X + arrowRect.Width + 6f, rect.Y + (height - textSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            label,
            textPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        return isOpen;
    }

    public bool CollapsingHeader(string label, UiTreeNodeFlags flags)
    {
        var defaultOpen = flags.HasFlag(UiTreeNodeFlags.DefaultOpen);
        return CollapsingHeader(label, defaultOpen);
    }

    public bool CollapsingHeader(string label, ref bool open, UiTreeNodeFlags flags = UiTreeNodeFlags.None)
    {
        if (!open)
        {
            return false;
        }

        var isOpen = CollapsingHeader(label, flags);
        SameLine();
        if (SmallButton($"{label}##close"))
        {
            open = false;
        }

        return isOpen;
    }

    public bool TreeNode(string label, bool defaultOpen = false)
    {
        label ??= "TreeNode";
        var rawId = $"{label}##tree";
        var id = _nextItemStorageId ?? ResolveId(rawId);
        _nextItemStorageId = null;

        var isOpen = _state.GetBool(id, defaultOpen);
        if (_hasNextItemOpen)
        {
            isOpen = _nextItemOpen;
            _state.SetBool(id, isOpen);
            _hasNextItemOpen = false;
        }
        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();
        var height = MathF.Max(textSize.Y, frameHeight);
        var arrowSize = MathF.Min(height, 12f);
        var available = GetContentRegionAvail();
        var minWidth = textSize.X + (ButtonPaddingX * 2f) + arrowSize + 6f;
        var width = MathF.Max(minWidth, available.X);
        var size = new UiVector2(width, height);
        var cursor = AdvanceCursor(size);
        var rect = new UiRect(cursor.X, cursor.Y, size.X, size.Y);

        var pressed = ButtonBehavior(rawId, rect, out var hovered, out _);
        if (pressed)
        {
            isOpen = !isOpen;
            _state.SetBool(id, isOpen);
        }
        _lastItemToggledOpen = pressed;

        var hoverBlend = AnimateFloat(
            $"{id}##tree_hover",
            hovered ? 1f : 0f,
            durationSeconds: 0.10f,
            easing: UiAnimationEasing.OutCubic
        );
        if (hoverBlend > 0.001f)
        {
            AddRectFilled(rect, ApplyAlpha(_theme.HeaderHovered, hoverBlend * 0.75f), _whiteTexture);
        }

        var arrowRect = new UiRect(rect.X + ButtonPaddingX, rect.Y + (height - arrowSize) * 0.5f, arrowSize, arrowSize);
        DrawTreeArrow(arrowRect, isOpen, id, _theme.Text);

        var textPos = new UiVector2(arrowRect.X + arrowRect.Width + 6f, rect.Y + (height - textSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            label,
            textPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        if (isOpen)
        {
            PushIndent();
        }

        return isOpen;
    }

    public bool TreeNodeEx(string label, UiTreeNodeFlags flags = UiTreeNodeFlags.None)
    {
        var defaultOpen = flags.HasFlag(UiTreeNodeFlags.DefaultOpen);
        return TreeNode(label, defaultOpen);
    }

    public bool TreeNodeV(string format, params object[] args)
    {
        var label = FormatInvariant(format, args);
        return TreeNode(label, false);
    }

    public bool TreeNodeExV(string format, UiTreeNodeFlags flags, params object[] args)
    {
        var label = FormatInvariant(format, args);
        return TreeNodeEx(label, flags);
    }

    public void TreePush(string id)
    {
        id ??= "TreePush";
        _lastItemId = ResolveId(id);
        _lastItemFlags = _currentItemFlags;
        PushIndent();
    }

    public float GetTreeNodeToLabelSpacing() => TreeIndent;

    public bool TreeNodePush(string label)
    {
        return TreeNode(label, true);
    }

    public void TreePop()
    {
        PopIndent();
    }

    private void DrawTreeArrow(UiRect rect, bool isOpen, string animationId, UiColor color)
    {
        var rotation = AnimateToggleRotationDegrees(
            $"{animationId}##tree_chevron",
            expanded: isOpen,
            collapsedDegrees: -90f,
            expandedDegrees: 0f,
            durationSeconds: 0.14f,
            easing: UiAnimationEasing.OutCubic
        );
        DrawChevronIcon(rect, rotation, scale: 2f / 3f, thickness: 1.2f, color: color, opticalOffsetY: _lineHeight * 0.06f);
    }
}

