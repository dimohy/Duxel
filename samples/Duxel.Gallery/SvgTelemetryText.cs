using Duxel.Core;

namespace Duxel.Gallery;

internal readonly struct SvgTelemetryText : IUiTextPresentation
{
    private static readonly string[] Digits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

    internal static void Show(UiImmediateContext ui, string text, bool settled)
    {
        if (settled)
            ui.Text(text);
        else
            ui.Text<SvgTelemetryText>(text);
    }

    public static void Render(UiControlContext context, string text, UiColor? color)
    {
        var size = Measure(context, text);
        var layout = context.PlaceText(size);
        if (!layout.Visible)
            return;

        var x = layout.Position.X;
        var foreground = color ?? context.Theme.Text;
        for (var offset = 0; offset < text.Length;)
        {
            var fragment = ReadFragment(text, ref offset);
            context.PaintText(fragment, layout with { Position = new(x, layout.Position.Y) }, foreground);
            x += context.MeasureText(fragment).X;
        }
    }

    public static UiVector2 Measure(UiControlContext context, string text)
    {
        var width = 0f;
        var height = 0f;
        for (var offset = 0; offset < text.Length;)
        {
            var size = context.MeasureText(ReadFragment(text, ref offset));
            width += size.X;
            height = MathF.Max(height, size.Y);
        }
        return new(width, height);
    }

    private static string ReadFragment(string text, ref int offset)
    {
        var start = offset++;
        var character = text[start];
        if (char.IsAsciiDigit(character))
            return Digits[character - '0'];

        while (offset < text.Length && !char.IsAsciiDigit(text[offset]))
            offset++;
        return text[start..offset];
    }
}
