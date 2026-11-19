using System.Buffers.Binary;
using System.Text;

namespace Duxel.Platform.Linux;

internal readonly record struct LinuxDesktopSettings(int? WindowScale, int? UnscaledDpi, int? Dpi, string? FontName)
{
    // XSETTINGS integer DPI is expressed in 1/1024 dots per inch.
    internal static LinuxDesktopSettings Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || data[0] > 1)
            throw new InvalidDataException("Invalid XSETTINGS header.");
        var reader = new Reader(data, data[0] == 0);
        reader.Take(8);
        var count = reader.UInt32();
        if (count > (uint)(data.Length / 12))
            throw new InvalidDataException("Invalid XSETTINGS record count.");
        int? scale = null, unscaled = null, dpi = null;
        string? font = null;
        for (uint i = 0; i < count; i++)
        {
            var type = reader.Take(1)[0];
            reader.Take(1);
            var nameLength = reader.UInt16();
            var name = reader.TakePadded(nameLength);
            reader.Take(4);
            switch (type)
            {
                case 0:
                    var value = unchecked((int)reader.UInt32());
                    if (name.SequenceEqual("Gdk/WindowScalingFactor"u8))
                        scale = value;
                    else if (name.SequenceEqual("Gdk/UnscaledDPI"u8))
                        unscaled = value;
                    else if (name.SequenceEqual("Xft/DPI"u8))
                        dpi = value;
                    break;
                case 1:
                    var text = reader.TakePadded(checked((int)reader.UInt32()));
                    if (name.SequenceEqual("Gtk/FontName"u8))
                        font = new UTF8Encoding(false, true).GetString(text);
                    break;
                case 2:
                    reader.Take(8);
                    break;
                default:
                    throw new InvalidDataException("Unsupported XSETTINGS value type.");
            }
        }
        return new(scale, unscaled, dpi, font);
    }

    internal (float Content, float Text) Resolve(float? resourceDpi, float? fontSize, bool absoluteFontSize)
    {
        if (WindowScale is <= 0 || UnscaledDpi is 0 or < -1 || Dpi is 0 or < -1)
            throw new InvalidDataException("Desktop scale and DPI must be positive.");
        var dpi = (Dpi is > 0 ? Dpi / 1024f : null) ?? resourceDpi ?? 96f * (WindowScale ?? 1);
        var content = WindowScale ?? dpi / 96f;
        var fontDpi = (UnscaledDpi is > 0 ? UnscaledDpi / 1024f : null) ?? dpi / content;
        // Fluent Body starts at 14 DIP; a desktop point size is converted at its native font DPI.
        var text = fontSize is { } size
            ? (absoluteFontSize ? size / content : size * fontDpi / 72f) / 14f
            : fontDpi / 96f;
        if (!float.IsFinite(content) || content <= 0 || !float.IsFinite(text) || text <= 0)
            throw new InvalidDataException("The desktop supplied an invalid typography scale.");
        return (content, text);
    }

    private ref struct Reader(ReadOnlySpan<byte> data, bool little)
    {
        private ReadOnlySpan<byte> _remaining = data;
        internal ReadOnlySpan<byte> Take(int count)
        {
            if (count < 0 || count > _remaining.Length)
                throw new InvalidDataException("Truncated XSETTINGS property.");
            var value = _remaining[..count];
            _remaining = _remaining[count..];
            return value;
        }
        internal ReadOnlySpan<byte> TakePadded(int count)
        {
            var value = Take(count);
            Take((4 - (count & 3)) & 3);
            return value;
        }
        internal uint UInt32() => little ? BinaryPrimitives.ReadUInt32LittleEndian(Take(4)) : BinaryPrimitives.ReadUInt32BigEndian(Take(4));
        internal ushort UInt16() => little ? BinaryPrimitives.ReadUInt16LittleEndian(Take(2)) : BinaryPrimitives.ReadUInt16BigEndian(Take(2));
    }
}
