using System.Buffers.Binary;
using System.Text;
using Duxel.Core;
using static Duxel.Platform.Linux.LinuxFontNative;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxFontResource
{
    private PlatformFontMetadata? _metadata;
    private double? _projectedBaseline;
    private bool _projectedBaselineRead;
    private string? _projectedVariations;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal string ProjectedVariations => _projectedVariations ??= ReadProjectedVariations();

    private string ReadProjectedVariations()
    {
        var face = hb_font_get_face(Shaper);
        var count = hb_ot_var_get_axis_count(face);
        if (count == 0)
            return string.Empty;
        var axes = new VariationAxis[count];
        fixed (VariationAxis* output = axes)
            if (hb_ot_var_get_axis_infos(face, 0, ref count, output) != count || count != axes.Length)
                throw new InvalidDataException("OpenType variation axes changed while reading an immutable face.");
        var values = new string[axes.Length];
        for (var index = 0; index < axes.Length; index++)
        {
            var axis = axes[index];
            var tag = new string([(char)(axis.Tag >> 24), (char)((axis.Tag >> 16) & 255),
                (char)((axis.Tag >> 8) & 255), (char)(axis.Tag & 255)]);
            values[index] = FormattableString.Invariant($"{tag.TrimEnd()}={axis.Default:R}");
        }
        return string.Join(',', values);
    }

    internal double? ProjectedBaseline
    {
        get
        {
            if (!_projectedBaselineRead)
            {
                _projectedBaseline = ReadProjectedBaseline();
                _projectedBaselineRead = true;
            }
            return _projectedBaseline;
        }
    }

    private double? ReadProjectedBaseline()
    {
        var face = hb_font_get_face(Shaper);
        var table = hb_face_reference_table(face, 0x4f532f32); // OS/2
        if (table == 0)
            throw new InvalidOperationException("HarfBuzz did not return an OS/2 table reference.");
        try
        {
            var data = hb_blob_get_data(table, out var length);
            if (length == 0)
                return null; // Keep native metrics for legacy faces without OS/2.
            if (length < 78 || data == null)
                throw new InvalidDataException("Truncated OpenType OS/2 line metrics.");

            var bytes = new ReadOnlySpan<byte>(data, 78);
            var unitsPerEm = hb_face_get_upem(face);
            if (unitsPerEm == 0)
                throw new InvalidDataException("OpenType units per em must be nonzero.");
            var useTypographicMetrics = (BinaryPrimitives.ReadUInt16BigEndian(bytes[62..]) & 128) != 0;
            if (useTypographicMetrics)
            {
                var ascent = BinaryPrimitives.ReadInt16BigEndian(bytes[68..]);
                var gap = BinaryPrimitives.ReadInt16BigEndian(bytes[72..]);
                return (ascent + gap) * (double)Size / unitsPerEm;
            }

            var horizontalHeight = ReadHorizontalLineHeight(face);
            if (!horizontalHeight.HasValue)
                return null;
            var windowsAscent = BinaryPrimitives.ReadUInt16BigEndian(bytes[74..]);
            var windowsDescent = BinaryPrimitives.ReadUInt16BigEndian(bytes[76..]);
            var windowsGap = Math.Max(0, horizontalHeight.Value - windowsAscent - windowsDescent);
            return (windowsAscent + windowsGap) * (double)Size / unitsPerEm;
        }
        finally { hb_blob_destroy(table); }
    }

    private static int? ReadHorizontalLineHeight(nint face)
    {
        var table = hb_face_reference_table(face, 0x68686561); // hhea
        if (table == 0)
            throw new InvalidOperationException("HarfBuzz did not return an hhea table reference.");
        try
        {
            var data = hb_blob_get_data(table, out var length);
            if (length == 0)
                return null;
            if (length < 10 || data == null)
                throw new InvalidDataException("Truncated OpenType hhea line metrics.");
            var bytes = new ReadOnlySpan<byte>(data, 10);
            return BinaryPrimitives.ReadInt16BigEndian(bytes[4..])
                - BinaryPrimitives.ReadInt16BigEndian(bytes[6..])
                + BinaryPrimitives.ReadInt16BigEndian(bytes[8..]);
        }
        finally { hb_blob_destroy(table); }
    }

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
