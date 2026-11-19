using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Duxel.Platform.Linux.LinuxFontNative;

namespace Duxel.Platform.Linux;

internal sealed unsafe partial class LinuxFontResource : IDisposable
{
    // Cairo may retain faces after scaled-font destruction. The callback owns both
    // the FT face and its library until Cairo releases its final reference.
    private struct FaceOwner
    {
        internal nint Library, Face;
        internal int Key;
    }

    internal nint Face
    {
        get; private set;
    }
    internal nint Scaled
    {
        get; private set;
    }
    internal nint Shaper
    {
        get; private set;
    }
    private nint _cairoFace;
    internal string Path
    {
        get;
    }
    internal float Size
    {
        get;
    }
    internal bool Oblique
    {
        get;
    }
    internal FontExtents Metrics
    {
        get;
    }

    internal LinuxFontResource(string path, float size, bool oblique)
    {
        Path = path;
        Size = size;
        Oblique = oblique;
        FaceOwner* owner = (FaceOwner*)NativeMemory.AllocZeroed((nuint)sizeof(FaceOwner));
        var attached = false;
        nint options = 0, shapingFace = 0;
        try
        {
            CheckFreeType(FT_Init_FreeType(out owner->Library), "initialize");
            CheckFreeType(FT_New_Face(owner->Library, path, 0, out owner->Face), "open " + path);
            Face = owner->Face;
            CheckFreeType(FT_Set_Char_Size(Face, 0, checked((nint)Math.Round(size * 64d)), 72, 72), "set size");
            _cairoFace = cairo_ft_font_face_create_for_ft_face(Face, 2); // FT_LOAD_NO_HINTING
            CheckCairo(cairo_font_face_status(_cairoFace), "create FreeType face");
            CheckCairo(cairo_font_face_set_user_data(_cairoFace, &owner->Key, owner, &ReleaseFace), "attach face lifetime");
            attached = true;
            options = cairo_font_options_create();
            cairo_font_options_set_antialias(options, 2); // GRAY
            cairo_font_options_set_hint_style(options, 1); // NONE
            cairo_font_options_set_hint_metrics(options, 1); // OFF
            var matrix = new Matrix(size) { Xy = oblique ? -size * LinuxPlatformTextBackend.ObliqueSlant : 0 };
            Scaled = cairo_scaled_font_create(_cairoFace, matrix, new Matrix(1), options);
            CheckCairo(cairo_scaled_font_status(Scaled), "create scaled font");
            cairo_scaled_font_extents(Scaled, out var metrics);
            CheckCairo(cairo_scaled_font_status(Scaled), "read font metrics");
            Metrics = metrics;
            shapingFace = hb_ft_face_create_referenced(Face);
            if (hb_face_get_glyph_count(shapingFace) == 0)
                throw new NotSupportedException("The explicit font must contain OpenType glyph tables.");
            Shaper = hb_font_create(shapingFace);
            if (Shaper == hb_font_get_empty())
                throw new OutOfMemoryException("HarfBuzz font allocation failed.");
            // OpenType shaping reads immutable tables; Cairo can change its FT size
            // without mutating the shaper's independent 26.6 coordinate scale.
            hb_ot_font_set_funcs(Shaper);
            var scale = checked((int)Math.Round(size * 64d));
            hb_font_set_scale(Shaper, scale, scale);
            if (oblique)
                hb_font_set_synthetic_slant(Shaper, LinuxPlatformTextBackend.ObliqueSlant);
        }
        catch
        {
            if (shapingFace != 0)
            {
                hb_face_destroy(shapingFace);
                shapingFace = 0;
            }
            Dispose();
            if (!attached)
                FreeFace(owner);
            throw;
        }
        finally
        {
            if (options != 0)
                cairo_font_options_destroy(options);
            if (shapingFace != 0)
                hb_face_destroy(shapingFace);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ReleaseFace(void* data) => FreeFace((FaceOwner*)data);

    private static void FreeFace(FaceOwner* owner)
    {
        if (owner->Face != 0)
            FT_Done_Face(owner->Face);
        if (owner->Library != 0)
            FT_Done_FreeType(owner->Library);
        NativeMemory.Free(owner);
    }

    internal static void CheckCairo(int status, string operation)
    {
        if (status != 0)
            throw new InvalidOperationException($"Cairo {operation} failed: {status}.");
    }

    private static void CheckFreeType(int status, string operation)
    {
        if (status != 0)
            throw new InvalidOperationException($"FreeType {operation} failed: {status}.");
    }

    public void Dispose()
    {
        if (Shaper != 0)
            hb_font_destroy(Shaper);
        if (Scaled != 0)
            cairo_scaled_font_destroy(Scaled);
        if (_cairoFace != 0)
            cairo_font_face_destroy(_cairoFace);
        Shaper = Scaled = Face = _cairoFace = 0;
    }
}
