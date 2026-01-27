namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool ColorButton(string id, UiColor color, UiVector2 size)
    {
        id ??= "ColorButton";

        var width = size.X > 0f ? size.X : 24f;
        var height = size.Y > 0f ? size.Y : 24f;
        var cursor = AdvanceCursor(new UiVector2(width, height));
        var rect = new UiRect(cursor.X, cursor.Y, width, height);

        var pressed = ButtonBehavior(id, rect, out var hovered, out var held);
        var bg = held ? _theme.ButtonActive : hovered ? _theme.ButtonHovered : _theme.Button;
        AddRectFilled(rect, bg, _whiteTexture);

        var inset = 2f;
        var colorRect = new UiRect(rect.X + inset, rect.Y + inset, rect.Width - inset * 2f, rect.Height - inset * 2f);
        AddRectFilled(colorRect, color, _whiteTexture);

        return pressed;
    }

    public bool ColorPicker3(string label, ref float r, ref float g, ref float b)
    {
        return ColorEdit3(label, ref r, ref g, ref b);
    }

    public bool ColorPicker4(string label, ref float r, ref float g, ref float b, ref float a)
    {
        return ColorEdit4(label, ref r, ref g, ref b, ref a);
    }

    public void SetColorEditOptions(uint flags)
    {
        _ = flags;
    }

    public bool ColorEdit3(string label, ref float r, ref float g, ref float b)
    {
        label ??= "Color";

        r = Math.Clamp(r, 0f, 1f);
        g = Math.Clamp(g, 0f, 1f);
        b = Math.Clamp(b, 0f, 1f);

        Text(label);

        var previewSize = new UiVector2(24f, 24f);
        var previewCursor = AdvanceCursor(previewSize);
        var previewRect = new UiRect(previewCursor.X, previewCursor.Y, previewSize.X, previewSize.Y);
        var previewColor = new UiColor(
            ((uint)(b * 255f) & 0xFF) |
            (((uint)(g * 255f) & 0xFF) << 8) |
            (((uint)(r * 255f) & 0xFF) << 16) |
            0xFF000000u
        );
        AddRectFilled(previewRect, previewColor, _whiteTexture);

        var changed = false;
        changed |= SliderFloat($"{label} R", ref r, 0f, 1f, 0.01f, "0.00");
        changed |= SliderFloat($"{label} G", ref g, 0f, 1f, 0.01f, "0.00");
        changed |= SliderFloat($"{label} B", ref b, 0f, 1f, 0.01f, "0.00");

        return changed;
    }

    public bool ColorEdit4(string label, ref float r, ref float g, ref float b, ref float a)
    {
        label ??= "Color";

        r = Math.Clamp(r, 0f, 1f);
        g = Math.Clamp(g, 0f, 1f);
        b = Math.Clamp(b, 0f, 1f);
        a = Math.Clamp(a, 0f, 1f);

        Text(label);

        var previewSize = new UiVector2(24f, 24f);
        var previewCursor = AdvanceCursor(previewSize);
        var previewRect = new UiRect(previewCursor.X, previewCursor.Y, previewSize.X, previewSize.Y);
        var previewColor = new UiColor(
            ((uint)(b * 255f) & 0xFF) |
            (((uint)(g * 255f) & 0xFF) << 8) |
            (((uint)(r * 255f) & 0xFF) << 16) |
            (((uint)(a * 255f) & 0xFF) << 24)
        );
        AddRectFilled(previewRect, previewColor, _whiteTexture);

        var changed = false;
        changed |= SliderFloat($"{label} R", ref r, 0f, 1f, 0.01f, "0.00");
        changed |= SliderFloat($"{label} G", ref g, 0f, 1f, 0.01f, "0.00");
        changed |= SliderFloat($"{label} B", ref b, 0f, 1f, 0.01f, "0.00");
        changed |= SliderFloat($"{label} A", ref a, 0f, 1f, 0.01f, "0.00");

        return changed;
    }
}
