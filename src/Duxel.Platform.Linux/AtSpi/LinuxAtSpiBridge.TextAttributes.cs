using System.Globalization;
using Duxel.Core;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxAtSpiBridge
{
    private readonly record struct ExportedTextAttributes(string? Family, int? Weight, string? Style, uint? Foreground, float? Size);

    private void PublishTextAttributeChanges(Node previous, Node current)
    {
        var old = previous.Element?.Node.TextAppearance;
        var next = current.Element?.Node.TextAppearance;
        if (previous.Element?.Node.TextInfo is not null && current.Element?.Node.TextInfo is not null
            && ((old is null) != (next is null) || old is not null && next is not null
                && old.Content.Text == next.Content.Text && !SameTextAttributes(old, next)))
            Event(current.Path, "TextAttributesChanged", "", 0, GVariant.Int(0));
    }

    private static ExportedTextAttributes AttributesAt(UiAccessibilityTextAppearance appearance, int offset)
    {
        var font = appearance.GetFontRangeAt(offset).Font;
        var paint = appearance.GetPaintRangeAt(offset);
        return new(font?.FamilyName, font?.Weight, font is null ? null
            : font.SimulatedOblique || font.Oblique == true ? "oblique" : font.Italic == true ? "italic"
            : font.Italic == false && font.Oblique == false ? "normal" : null,
            paint.Foreground?.Rgba & 0x00ffffff, paint.Size?.Points);
    }

    private static (int Start, int End, ExportedTextAttributes Attributes) AttributeSegment(UiAccessibilityTextAppearance appearance, int offset)
    {
        var font = appearance.GetFontRangeAt(offset);
        var paint = appearance.GetPaintRangeAt(offset);
        return (Math.Max(font.Start, paint.Start), Math.Min(font.End, paint.End), AttributesAt(appearance, offset));
    }

    private static (int Start, int End, ExportedTextAttributes Attributes) AttributeRange(UiAccessibilityTextAppearance appearance, int offset)
    {
        var range = AttributeSegment(appearance, offset);
        while (range.Start > 0)
        {
            var previous = AttributeSegment(appearance, range.Start - 1);
            if (previous.Attributes != range.Attributes)
                break;
            range.Start = previous.Start;
        }
        while (range.End < appearance.Content.CharacterCount)
        {
            var next = AttributeSegment(appearance, range.End);
            if (next.Attributes != range.Attributes)
                break;
            range.End = next.End;
        }
        return range;
    }

    private static bool SameTextAttributes(UiAccessibilityTextAppearance old, UiAccessibilityTextAppearance next)
    {
        if (ReferenceEquals(old, next))
            return true;
        if (old.HasUniformPaint != next.HasUniformPaint)
            return false;
        var offset = 0;
        do
        {
            var a = AttributeSegment(old, offset);
            var b = AttributeSegment(next, offset);
            if (a.Attributes != b.Attributes)
                return false;
            offset = Math.Min(a.End, b.End);
        } while (offset < old.Content.CharacterCount);
        return true;
    }

    private static string FontAttributeValue(UiAccessibilityTextAppearance appearance, int offset, string name)
    {
        var fields = AttributesAt(appearance, offset);
        return name switch
        {
            "family-name" => fields.Family ?? "",
            "weight" => fields.Weight?.ToString(CultureInfo.InvariantCulture) ?? "",
            "style" => fields.Style ?? "",
            "fg-color" => TextForeground(fields.Foreground) ?? "",
            "size" => fields.Size?.ToString(CultureInfo.InvariantCulture) ?? "",
            _ => ""
        };
    }

    private static GVariant FontAttributeSet(UiAccessibilityTextAppearance appearance, ExportedTextAttributes fields, bool defaults)
    {
        List<GVariant> values = [];
        void Add(string name, string? value)
        {
            if (value is not null)
                values.Add(GVariant.DictEntry(GVariant.String(name), GVariant.String(value)));
        }
        Add("family-name", fields.Family);
        Add("weight", fields.Weight?.ToString(CultureInfo.InvariantCulture));
        Add("style", fields.Style);
        if (!appearance.HasUniformPaint)
        {
            Add("fg-color", TextForeground(fields.Foreground));
            Add("size", fields.Size?.ToString(CultureInfo.InvariantCulture));
        }
        else if (defaults)
        {
            Add("fg-color", TextForeground(appearance.Foreground.Rgba & 0x00ffffff));
            Add("size", appearance.Size?.Points.ToString(CultureInfo.InvariantCulture));
        }
        return GVariant.Array("{ss}", values.ToArray());
    }

    private static UiAccessibilityTextAppearance TextAppearance(Node node) => node.Element?.Node.TextAppearance
        ?? throw new ProtocolError("org.freedesktop.DBus.Error.NotSupported", "This text does not publish paint attributes.");

    private static string? TextForeground(uint? color) => color is { } rgb
        ? string.Create(CultureInfo.InvariantCulture, $"{(rgb & 255) * 257},{((rgb >> 8) & 255) * 257},{((rgb >> 16) & 255) * 257}") : null;

    private static GVariant TextAttributeSet(Node node, bool includeDefaults)
        => FontAttributeSet(TextAppearance(node), default, includeDefaults);
}
