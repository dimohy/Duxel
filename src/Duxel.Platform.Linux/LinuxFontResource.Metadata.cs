using System.Buffers.Binary;
using System.Text;
using Duxel.Core;
using static Duxel.Platform.Linux.LinuxFontNative;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxFontResource
{
    private PlatformFontMetadata? _metadata;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal PlatformFontMetadata GetMetadata(string languageName, nint language)
    {
        if (_metadata is { } retained && retained.NameLanguage == languageName)
            return retained;
        var face = hb_font_get_face(Shaper); // Borrowed from the exact font used for shaping.
        string? Name(uint id)
        {
            uint size = 0;
            var length = hb_ot_name_get_utf8(face, id, language, &size, null);
            if (length == 0)
                return null;
            var bytes = new byte[checked((int)length + 1)];
            size = (uint)bytes.Length;
            fixed (byte* buffer = bytes)
                if (hb_ot_name_get_utf8(face, id, language, &size, buffer) != length || size != length || bytes[^1] != 0)
                    throw new InvalidDataException("OpenType name length changed while reading an immutable face.");
            var name = StrictUtf8.GetString(bytes.AsSpan(0, (int)length));
            if (name.Contains('\0'))
                throw new InvalidDataException("OpenType names cannot contain embedded NUL.");
            return name;
        }
        int? weight = null;
        bool? italic = null;
        bool? oblique = null;
        var table = hb_face_reference_table(face, 0x4f532f32); // OS/2
        if (table == 0)
            throw new InvalidOperationException("HarfBuzz did not return an OS/2 table reference.");
        try
        {
            var data = hb_blob_get_data(table, out var length);
            if (length > 0)
            {
                if (length < 68 || data == null)
                    throw new InvalidDataException("Truncated OpenType OS/2 metadata.");
                var bytes = new ReadOnlySpan<byte>(data, checked((int)length));
                weight = BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]);
                if (weight is < 1 or > 1000)
                    throw new InvalidDataException("OpenType font weight must be in 1..1000.");
                italic = (BinaryPrimitives.ReadUInt16BigEndian(bytes[62..]) & 1) != 0;
                oblique = BinaryPrimitives.ReadUInt16BigEndian(bytes) >= 4
                    && (BinaryPrimitives.ReadUInt16BigEndian(bytes[62..]) & 512) != 0;
            }
        }
        finally { hb_blob_destroy(table); }
        return _metadata = new(Name(1), Name(2), weight, italic, Oblique, languageName)
        {
            Oblique = oblique
        };
    }
}
