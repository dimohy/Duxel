using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

/// <summary>Owns one non-floating GVariant reference. Factory containers consume their child wrappers.</summary>
internal sealed unsafe partial class GVariant : IDisposable
{
    private const string Library = "libglib-2.0.so.0";
    private static readonly System.Text.UTF8Encoding Utf8 = new(false, true);
    internal nint Handle
    {
        get; private set;
    }
    private GVariant(nint value, bool sink)
    {
        if (value == 0)
            throw new InvalidDataException("GVariant construction returned null.");
        Handle = sink ? g_variant_ref_sink(value) : value;
    }
    internal static GVariant Owned(nint value) => new(value, false);
    internal static GVariant String(string value)
    {
        if (value.Contains('\0'))
            throw new ArgumentException("D-Bus strings cannot contain NUL.", nameof(value));
        Utf8.GetByteCount(value);
        return new(g_variant_new_string(value), true);
    }
    internal static GVariant Path(string value) => new(g_variant_new_object_path(value), true);
    internal static GVariant Int(int value) => new(g_variant_new_int32(value), true);
    internal static GVariant UInt(uint value) => new(g_variant_new_uint32(value), true);
    internal static GVariant Short(short value) => new(g_variant_new_int16(value), true);
    internal static GVariant Bool(bool value) => new(g_variant_new_boolean(value ? 1 : 0), true);
    internal static GVariant Double(double value) => new(g_variant_new_double(value), true);
    internal static GVariant Box(GVariant value)
    {
        using (value)
            return new(g_variant_new_variant(value.Handle), true);
    }
    internal static GVariant Referenced(nint value) => Owned(g_variant_ref(value));
    internal static GVariant Tuple(params GVariant[] children) => Container(null, children);
    internal static GVariant Array(string elementType, params GVariant[] children) => Container(elementType, children);
    private static GVariant Container(string? type, GVariant[] children)
    {
        var handles = new nint[children.Length];
        for (var i = 0; i < handles.Length; i++)
            handles[i] = children[i].Handle;
        try
        {
            fixed (nint* data = handles)
            {
                if (type is null)
                    return new(g_variant_new_tuple(data, (nuint)handles.Length), true);
                var nativeType = g_variant_type_new(type);
                try
                {
                    return new(g_variant_new_array(nativeType, data, (nuint)handles.Length), true);
                }
                finally { g_variant_type_free(nativeType); }
            }
        }
        finally { foreach (var child in children) child.Dispose(); }
    }
    internal static GVariant DictEntry(GVariant key, GVariant value)
    {
        using (key)
        using (value)
            return new(g_variant_new_dict_entry(key.Handle, value.Handle), true);
    }
    internal GVariant Child(int index) => Owned(g_variant_get_child_value(Handle, (nuint)index));
    internal int AsInt() => g_variant_get_int32(Handle);
    internal bool AsBool() => g_variant_get_boolean(Handle) != 0;
    internal uint AsUInt() => g_variant_get_uint32(Handle);
    internal double AsDouble() => g_variant_get_double(Handle);
    internal string AsString() => GioNative.Text(g_variant_get_string(Handle, null));
    internal nint Release()
    {
        var value = Handle;
        Handle = 0;
        return value;
    }
    public void Dispose()
    {
        if (Handle != 0)
            g_variant_unref(Handle);
        Handle = 0;
    }

    [LibraryImport(Library)] private static partial nint g_variant_ref_sink(nint value);
    [LibraryImport(Library)] private static partial nint g_variant_ref(nint value);
    [LibraryImport(Library)] private static partial void g_variant_unref(nint value);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial nint g_variant_new_string(string value);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial nint g_variant_new_object_path(string value);
    [LibraryImport(Library)] private static partial nint g_variant_new_int32(int value);
    [LibraryImport(Library)] private static partial nint g_variant_new_uint32(uint value);
    [LibraryImport(Library)] private static partial nint g_variant_new_int16(short value);
    [LibraryImport(Library)] private static partial nint g_variant_new_boolean(int value);
    [LibraryImport(Library)] private static partial nint g_variant_new_double(double value);
    [LibraryImport(Library)] private static partial double g_variant_get_double(nint value);
    [LibraryImport(Library)] private static partial nint g_variant_new_variant(nint value);
    [LibraryImport(Library)] private static partial nint g_variant_new_tuple(nint* children, nuint count);
    [LibraryImport(Library)] private static partial nint g_variant_new_array(nint type, nint* children, nuint count);
    [LibraryImport(Library)] private static partial nint g_variant_new_dict_entry(nint key, nint value);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] private static partial nint g_variant_type_new(string type);
    [LibraryImport(Library)] private static partial void g_variant_type_free(nint type);
    [LibraryImport(Library)] private static partial nint g_variant_get_child_value(nint value, nuint index);
    [LibraryImport(Library)] private static partial int g_variant_get_int32(nint value);
    [LibraryImport(Library)] private static partial int g_variant_get_boolean(nint value);
    [LibraryImport(Library)] private static partial uint g_variant_get_uint32(nint value);
    [LibraryImport(Library)] private static partial nint g_variant_get_string(nint value, nuint* length);
}
