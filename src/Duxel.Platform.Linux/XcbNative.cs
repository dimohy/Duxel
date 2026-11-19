using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

internal static unsafe partial class XcbNative
{
    private const string Library = "libxcb.so.1";
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Cookie
    {
        internal readonly uint Sequence;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct ScreenIterator
    {
        internal void* Data;
        internal int Remaining, Index;
    }
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct Event
    {
        [FieldOffset(0)] internal byte Type;
        [FieldOffset(4)] internal uint A;
        [FieldOffset(8)] internal uint B;
        [FieldOffset(12)] internal uint C;
        [FieldOffset(16)] internal uint D;
        [FieldOffset(20)] internal uint E;
        [FieldOffset(24)] internal uint F;
        [FieldOffset(16)] internal byte State;
    }
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct PropertyReply
    {
        [FieldOffset(1)] internal byte Format;
        [FieldOffset(8)] internal uint Type;
        [FieldOffset(12)] internal uint BytesAfter;
        [FieldOffset(16)] internal uint Count;
    }

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint xcb_connect(string name, out int screen);
    [LibraryImport(Library)] internal static partial void xcb_disconnect(nint connection);
    [LibraryImport(Library)] internal static partial int xcb_connection_has_error(nint connection);
    [LibraryImport(Library)] internal static partial nint xcb_get_setup(nint connection);
    [LibraryImport(Library)] internal static partial ScreenIterator xcb_setup_roots_iterator(nint setup);
    [LibraryImport(Library)] internal static partial void xcb_screen_next(ref ScreenIterator iterator);
    [LibraryImport(Library)] internal static partial int xcb_get_file_descriptor(nint connection);
    [LibraryImport(Library)] internal static partial uint xcb_generate_id(nint connection);
    [LibraryImport(Library)] internal static partial uint xcb_get_maximum_request_length(nint connection);
    [LibraryImport(Library)] internal static partial int xcb_flush(nint connection);
    [LibraryImport(Library)] internal static partial Event* xcb_poll_for_event(nint connection);
    [LibraryImport(Library)] internal static partial void* xcb_request_check(nint connection, Cookie cookie);
    [LibraryImport(Library)] internal static partial Cookie xcb_create_window_checked(nint c, byte depth, uint id, uint parent, short x, short y, ushort width, ushort height, ushort border, ushort windowClass, uint visual, uint mask, uint* values);
    [LibraryImport(Library)] internal static partial Cookie xcb_change_window_attributes_checked(nint c, uint window, uint mask, uint* values);
    [LibraryImport(Library)] internal static partial Cookie xcb_intern_atom(nint c, byte onlyIfExists, ushort length, byte* name);
    [LibraryImport(Library)] internal static partial void* xcb_intern_atom_reply(nint c, Cookie cookie, void** error);
    [LibraryImport(Library)] internal static partial Cookie xcb_set_selection_owner_checked(nint c, uint owner, uint selection, uint time);
    [LibraryImport(Library)] internal static partial Cookie xcb_get_selection_owner(nint c, uint selection);
    [LibraryImport(Library)] internal static partial void* xcb_get_selection_owner_reply(nint c, Cookie cookie, void** error);
    [LibraryImport(Library)] internal static partial Cookie xcb_convert_selection_checked(nint c, uint window, uint selection, uint target, uint property, uint time);
    [LibraryImport(Library)] internal static partial Cookie xcb_change_property_checked(nint c, byte mode, uint window, uint property, uint type, byte format, uint count, void* data);
    [LibraryImport(Library)] internal static partial Cookie xcb_delete_property_checked(nint c, uint window, uint property);
    [LibraryImport(Library)] internal static partial Cookie xcb_get_property(nint c, byte delete, uint window, uint property, uint type, uint offset, uint length);
    [LibraryImport(Library)] internal static partial PropertyReply* xcb_get_property_reply(nint c, Cookie cookie, void** error);
    [LibraryImport(Library)] internal static partial Cookie xcb_send_event_checked(nint c, byte propagate, uint destination, uint mask, Event* value);
    [LibraryImport(Library)] internal static partial Cookie xcb_set_input_focus_checked(nint c, byte revertTo, uint focus, uint time);
    [LibraryImport(Library)] internal static partial Cookie xcb_get_input_focus(nint c);
    [LibraryImport(Library)] internal static partial void* xcb_get_input_focus_reply(nint c, Cookie cookie, void** error);
    [LibraryImport("libc.so.6")] internal static partial void free(void* pointer);
}
