using System.Runtime.InteropServices;
using Duxel.Svg.Managed;

namespace Duxel.Svg;

internal static unsafe partial class SvgGdkImageDecoder
{
    internal static SvgDecodedImage Decode(byte[] bytes)
    {
        var loader = gdk_pixbuf_loader_new();
        if (loader == 0) throw new IOException("GdkPixbuf could not create an embedded-image loader.");
        var closed = false;
        try
        {
            nint error = 0;
            int written;
            fixed (byte* data = bytes) written = gdk_pixbuf_loader_write(loader, data, (nuint)bytes.Length, &error);
            Check(written, ref error);
            var completed = gdk_pixbuf_loader_close(loader, &error);
            closed = true;
            Check(completed, ref error);
            var image = gdk_pixbuf_loader_get_pixbuf(loader);
            if (image == 0) throw new InvalidDataException("GdkPixbuf returned no embedded image.");
            var width = gdk_pixbuf_get_width(image);
            var height = gdk_pixbuf_get_height(image);
            var channels = gdk_pixbuf_get_n_channels(image);
            var alpha = gdk_pixbuf_get_has_alpha(image) != 0;
            if (width <= 0 || height <= 0 || (long)width * height > 16 * 1024 * 1024 ||
                gdk_pixbuf_get_bits_per_sample(image) != 8 || gdk_pixbuf_get_colorspace(image) != 0 || channels != (alpha ? 4 : 3))
                throw new InvalidDataException("Embedded SVG bitmap must contain bounded 8-bit RGB or RGBA pixels.");
            var stride = gdk_pixbuf_get_rowstride(image);
            if (stride < checked(width * channels)) throw new InvalidDataException("Embedded bitmap row stride is invalid.");
            var source = (byte*)gdk_pixbuf_get_pixels(image);
            if (source == null) throw new InvalidDataException("Embedded bitmap has no pixel buffer.");
            var pixels = new byte[checked(width * height * 4)];
            for (var row = 0; row < height; row++)
            {
                var input = new ReadOnlySpan<byte>(source + checked(row * stride), checked(width * channels));
                var destination = pixels.AsSpan(row * width * 4, width * 4);
                if (alpha) input.CopyTo(destination);
                else for (var column = 0; column < width; column++)
                {
                    input.Slice(column * 3, 3).CopyTo(destination.Slice(column * 4, 3));
                    destination[column * 4 + 3] = 255;
                }
            }
            return new(width, height, pixels);
        }
        finally
        {
            if (!closed)
            {
                // A failed write still requires Close before releasing the loader; its original error propagates.
                nint cleanupError = 0;
                gdk_pixbuf_loader_close(loader, &cleanupError);
                if (cleanupError != 0) g_error_free(cleanupError);
            }
            g_object_unref(loader);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeError { internal uint Domain; internal int Code; internal nint Message; }
    private static void Check(int success, ref nint error)
    {
        if (error != 0)
        {
            var owned = error;
            error = 0;
            try { throw new InvalidDataException(Marshal.PtrToStringUTF8(((NativeError*)owned)->Message)); }
            finally { g_error_free(owned); }
        }
        if (success == 0) throw new IOException("GdkPixbuf failed without returning an image error.");
    }
    private const string Library = "libgdk_pixbuf-2.0.so.0";
    [LibraryImport(Library)] private static partial nint gdk_pixbuf_loader_new();
    [LibraryImport(Library)] private static partial int gdk_pixbuf_loader_write(nint loader, byte* data, nuint size, nint* error);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_loader_close(nint loader, nint* error);
    [LibraryImport(Library)] private static partial nint gdk_pixbuf_loader_get_pixbuf(nint loader);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_width(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_height(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_n_channels(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_has_alpha(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_bits_per_sample(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_colorspace(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_rowstride(nint image);
    [LibraryImport(Library)] private static partial nint gdk_pixbuf_get_pixels(nint image);
    [LibraryImport("libgobject-2.0.so.0")] private static partial void g_object_unref(nint value);
    [LibraryImport("libglib-2.0.so.0")] private static partial void g_error_free(nint error);
}
