namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public void Image(UiTextureId textureId, UiVector2 size, UiColor? tint = null)
    {
        var width = MathF.Max(0f, size.X);
        var height = MathF.Max(0f, size.Y);
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);
        var color = tint ?? new UiColor(0xFFFFFFFF);
        AddRectFilled(rect, color, textureId);
    }

    public void ImageWithBg(UiTextureId textureId, UiVector2 size, UiColor background, UiColor? tint = null)
    {
        var width = MathF.Max(0f, size.X);
        var height = MathF.Max(0f, size.Y);
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        AddRectFilled(rect, background, _whiteTexture);

        var color = tint ?? new UiColor(0xFFFFFFFF);
        AddRectFilled(rect, color, textureId);
    }

    public bool ImageButton(string id, UiTextureId textureId, UiVector2 size, UiColor? tint = null)
    {
        id ??= "ImageButton";

        var width = MathF.Max(0f, size.X);
        var height = MathF.Max(0f, size.Y);
        var totalSize = new UiVector2(width, height);
        var cursor = AdvanceCursor(totalSize);
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        var pressed = ButtonBehavior(id, rect, out var hovered, out var held);
        var background = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        AddRectFilled(rect, background, _whiteTexture);

        var color = tint ?? new UiColor(0xFFFFFFFF);
        AddRectFilled(rect, color, textureId);

        return pressed;
    }
}
