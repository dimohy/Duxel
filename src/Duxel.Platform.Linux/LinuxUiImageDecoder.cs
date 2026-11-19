using System.Runtime.InteropServices;
using System.Buffers.Binary;
using Duxel.Core;

namespace Duxel.Platform.Linux;

/// <summary>Decodes the first bitmap frame using installed GdkPixbuf codecs and copies straight RGBA pixels.</summary>
public sealed unsafe partial class LinuxUiImageDecoder : IUiImageDecoder
{
    public static LinuxUiImageDecoder Shared { get; } = new();

    public bool CanDecode(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico";
    }

    public UiImageData Decode(string path)
    {
        if (!OperatingSystem.IsLinux() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The GdkPixbuf image decoder requires 64-bit Linux.");
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!CanDecode(path))
            throw new NotSupportedException("Unsupported extension for LinuxUiImageDecoder: " + Path.GetExtension(path));
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Image file not found.", fullPath);
        nint error = 0;
        var icon = Path.GetExtension(fullPath).Equals(".ico", StringComparison.OrdinalIgnoreCase)
            ? ReadIcon(fullPath) : default;
        var image = icon.Data is null ? gdk_pixbuf_new_from_file(fullPath, &error) : LoadBuffer(icon.Data, icon.Codec, &error);
        try
        {
            if (error != 0)
            {
                var detail = Marshal.PtrToStringUTF8(((NativeError*)error)->Message);
                throw new InvalidDataException("GdkPixbuf could not decode the image: " + detail);
            }
            if (image == 0)
                throw new IOException("GdkPixbuf returned no image and no error.");
            var width = gdk_pixbuf_get_width(image);
            var height = gdk_pixbuf_get_height(image);
            var channels = gdk_pixbuf_get_n_channels(image);
            var alpha = gdk_pixbuf_get_has_alpha(image) != 0;
            if (width <= 0 || height <= 0 || gdk_pixbuf_get_bits_per_sample(image) != 8
                || gdk_pixbuf_get_colorspace(image) != 0 || channels != (alpha ? 4 : 3))
                throw new NotSupportedException("The decoded image must contain 8-bit RGB or RGBA pixels.");
            var stride = gdk_pixbuf_get_rowstride(image);
            var rowBytes = checked(width * channels);
            var required = checked((nuint)((long)(height - 1) * stride + rowBytes));
            var source = gdk_pixbuf_read_pixels(image);
            if (stride < rowBytes || source == null || gdk_pixbuf_get_byte_length(image) < required)
                throw new InvalidDataException("GdkPixbuf returned an invalid pixel layout.");
            var rgba = new byte[checked(width * height * 4)];
            for (var y = 0; y < height; y++)
            {
                var row = new ReadOnlySpan<byte>(source + checked((long)y * stride), rowBytes);
                var destination = rgba.AsSpan(checked(y * width * 4), checked(width * 4));
                if (alpha)
                    row.CopyTo(destination);
                else
                {
                    for (var x = 0; x < width; x++)
                    {
                        row.Slice(x * 3, 3).CopyTo(destination.Slice(x * 4, 3));
                        destination[x * 4 + 3] = 255;
                    }
                }
            }
            return new(width, height, rgba);
        }
        finally
        {
            if (image != 0)
                g_object_unref(image);
            if (error != 0)
                g_error_free(error);
        }
    }

    private static (byte[]? Data, string Codec) ReadIcon(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 22 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)) != 1)
            throw new InvalidDataException("Invalid ICO directory.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4));
        if (count == 0 || bytes.Length < 6 + count * 16)
            throw new InvalidDataException("Incomplete ICO directory.");
        var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14));
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(18));
        if (offset < 6 + count * 16 || length == 0 || (ulong)offset + length > (ulong)bytes.Length)
            throw new InvalidDataException("Invalid ICO frame bounds.");
        var frame = bytes.AsSpan((int)offset, (int)length);
        // ICO has two explicit encodings: DIB and an embedded PNG frame.
        ReadOnlySpan<byte> png = [137, 80, 78, 71, 13, 10, 26, 10];
        if (frame.StartsWith(png))
            return (frame.ToArray(), "png");
        // Present only directory entry zero, keeping first-frame semantics for multi-size DIB icons.
        var single = new byte[checked(22 + frame.Length)];
        bytes.AsSpan(0, 22).CopyTo(single);
        BinaryPrimitives.WriteUInt16LittleEndian(single.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(single.AsSpan(18), 22);
        frame.CopyTo(single.AsSpan(22));
        return (single, "ico");
    }

    private static nint LoadBuffer(ReadOnlySpan<byte> bytes, string codec, nint* error)
    {
        var loader = gdk_pixbuf_loader_new_with_type(codec, error);
        if (loader == 0)
            return 0;
        var closed = false;
        try
        {
            fixed (byte* pointer = bytes)
                if (gdk_pixbuf_loader_write(loader, pointer, (nuint)bytes.Length, error) == 0)
                    return 0;
            closed = true;
            if (gdk_pixbuf_loader_close(loader, error) == 0)
                return 0;
            var image = gdk_pixbuf_loader_get_pixbuf(loader);
            return image == 0 ? 0 : g_object_ref(image);
        }
        finally
        {
            if (!closed)
            {
                nint closeError = 0;
                gdk_pixbuf_loader_close(loader, &closeError);
                if (closeError != 0)
                    g_error_free(closeError);
            }
            g_object_unref(loader);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeError
    {
        internal uint Domain;
        internal int Code;
        internal nint Message;
    }

    private const string Library = "libgdk_pixbuf-2.0.so.0";
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial nint gdk_pixbuf_new_from_file(string path, nint* error);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial nint gdk_pixbuf_loader_new_with_type(string type, nint* error);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_loader_write(nint loader, byte* data, nuint length, nint* error);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_loader_close(nint loader, nint* error);
    [LibraryImport(Library)] private static partial nint gdk_pixbuf_loader_get_pixbuf(nint loader);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_width(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_height(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_n_channels(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_has_alpha(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_bits_per_sample(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_colorspace(nint image);
    [LibraryImport(Library)] private static partial int gdk_pixbuf_get_rowstride(nint image);
    [LibraryImport(Library)] private static partial nuint gdk_pixbuf_get_byte_length(nint image);
    [LibraryImport(Library)] private static partial byte* gdk_pixbuf_read_pixels(nint image);
    [LibraryImport("libgobject-2.0.so.0")] private static partial void g_object_unref(nint instance);
    [LibraryImport("libgobject-2.0.so.0")] private static partial nint g_object_ref(nint instance);
    [LibraryImport("libglib-2.0.so.0")] private static partial void g_error_free(nint error);
}
