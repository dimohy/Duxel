namespace Dux.Core;

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
        var width = textSize.X + (ButtonPaddingX * 2f) + 14f;
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

        var bg = held ? _theme.HeaderActive : hovered ? _theme.HeaderHovered : _theme.Header;
        AddRectFilled(rect, bg, _whiteTexture);

        var arrowText = isOpen ? "v" : ">";
        var arrowSize = UiTextBuilder.MeasureText(_fontAtlas, arrowText, _textSettings, _lineHeight);
        var arrowPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (height - arrowSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            arrowText,
            arrowPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        var textPos = new UiVector2(arrowPos.X + arrowSize.X + 6f, rect.Y + (height - textSize.Y) * 0.5f);
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
        var width = textSize.X + (ButtonPaddingX * 2f) + 14f;
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

        if (hovered)
        {
            AddRectFilled(rect, _theme.HeaderHovered, _whiteTexture);
        }

        var arrowText = isOpen ? "v" : ">";
        var arrowSize = UiTextBuilder.MeasureText(_fontAtlas, arrowText, _textSettings, _lineHeight);
        var arrowPos = new UiVector2(rect.X + ButtonPaddingX, rect.Y + (height - arrowSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            arrowText,
            arrowPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        var textPos = new UiVector2(arrowPos.X + arrowSize.X + 6f, rect.Y + (height - textSize.Y) * 0.5f);
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
}
