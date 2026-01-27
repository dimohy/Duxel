namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginListBox(string label, UiVector2 size, int itemsCount = -1)
    {
        return ListBoxHeader(label, size, itemsCount);
    }

    public void EndListBox()
    {
        ListBoxFooter();
    }

    public bool ListBoxHeader(string label, UiVector2 size, int itemsCount = -1)
    {
        label ??= "ListBox";
        var id = ResolveId(label);

        var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
        var frameHeight = GetFrameHeight();

        var width = size.X > 0f ? size.X : ResolveItemWidth(InputWidth);
        var height = size.Y > 0f ? size.Y : frameHeight * Math.Max(1, itemsCount > 0 ? itemsCount : 6);

        var totalSize = new UiVector2(textSize.X + ItemSpacingX + width, MathF.Max(textSize.Y, height));
        var cursor = AdvanceCursor(totalSize);

        var labelPos = new UiVector2(cursor.X, cursor.Y + (totalSize.Y - textSize.Y) * 0.5f);
        _builder.AddText(
            _fontAtlas,
            label,
            labelPos,
            _theme.Text,
            _fontTexture,
            CurrentClipRect,
            _textSettings,
            _lineHeight
        );

        var listRect = new UiRect(cursor.X + textSize.X + ItemSpacingX, cursor.Y + (totalSize.Y - height) * 0.5f, width, height);
        AddRectFilled(listRect, _theme.FrameBg, _whiteTexture);

        var hovered = IsHovering(listRect);
        var scrollY = _state.GetScrollY(id);
        if (hovered && MathF.Abs(_mouseWheel) > 0.001f)
        {
            scrollY = MathF.Max(0f, scrollY - (_mouseWheel * frameHeight * 3f));
        }

        PushListBoxLayout(id, listRect, scrollY);
        return true;
    }

    public void ListBoxFooter()
    {
        PopListBoxLayout();
    }
}
