using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

internal static unsafe partial class LinuxFontNative
{
    [LibraryImport(HarfBuzz)] internal static partial nint hb_font_get_face(nint font);
    [LibraryImport(HarfBuzz, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint hb_language_from_string(string language, int length);
    [LibraryImport(HarfBuzz)] internal static partial nint hb_language_to_string(nint language);
    [LibraryImport(HarfBuzz)] internal static partial uint hb_ot_name_get_utf8(nint face, uint id, nint language, uint* size, byte* text);
    [LibraryImport(HarfBuzz)] internal static partial nint hb_face_reference_table(nint face, uint tag);
    [LibraryImport(HarfBuzz)] internal static partial byte* hb_blob_get_data(nint blob, out uint length);
    [LibraryImport(HarfBuzz)] internal static partial void hb_blob_destroy(nint blob);
}
