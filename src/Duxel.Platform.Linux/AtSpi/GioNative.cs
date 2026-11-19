using System.Runtime.InteropServices;

namespace Duxel.Platform.Linux;

internal static unsafe partial class GioNative
{
    private const string Gio = "libgio-2.0.so.0", Glib = "libglib-2.0.so.0", GObject = "libgobject-2.0.so.0";
    [StructLayout(LayoutKind.Sequential)]
    internal struct VTable
    {
        internal nint Method, GetProperty, SetProperty;
        private fixed long _reserved[8];
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Error
    {
        internal uint Domain;
        internal int Code;
        internal nint Message;
    }

    internal static void CheckError(ref nint error, string operation)
    {
        if (error == 0)
            return;
        var value = *(Error*)error;
        var message = Marshal.PtrToStringUTF8(value.Message);
        g_error_free(error);
        error = 0;
        throw new IOException($"AT-SPI {operation} failed ({value.Domain}/{value.Code}): {message}");
    }
    internal static string Text(nint pointer) => Marshal.PtrToStringUTF8(pointer) ?? throw new InvalidDataException("GIO returned a null string.");

    [LibraryImport(Gio)] internal static partial nint g_dbus_address_get_for_bus_sync(int busType, nint cancellable, nint* error);
    [LibraryImport(Gio, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint g_dbus_connection_new_for_address_sync(string address, uint flags, nint observer, nint cancellable, nint* error);
    [LibraryImport(Gio)] internal static partial nint g_dbus_connection_get_unique_name(nint connection);
    [LibraryImport(Gio)] internal static partial void g_dbus_connection_set_exit_on_close(nint connection, int value);
    [LibraryImport(Gio)] internal static partial int g_dbus_connection_is_closed(nint connection);
    [LibraryImport(Gio)] internal static partial int g_dbus_connection_close_sync(nint connection, nint cancellable, nint* error);
    [LibraryImport(Gio, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint g_dbus_connection_call_sync(nint connection, string destination, string path, string @interface, string method, nint parameters, nint replyType, uint flags, int timeout, nint cancellable, nint* error);
    [LibraryImport(Gio, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint g_dbus_node_info_new_for_xml(string xml, nint* error);
    [LibraryImport(Gio)] internal static partial void g_dbus_node_info_unref(nint info);
    [LibraryImport(Gio, StringMarshalling = StringMarshalling.Utf8)] internal static partial nint g_dbus_node_info_lookup_interface(nint info, string name);
    [LibraryImport(Gio, StringMarshalling = StringMarshalling.Utf8)] internal static partial uint g_dbus_connection_register_object(nint connection, string path, nint info, VTable* vtable, nint data, nint destroy, nint* error);
    [LibraryImport(Gio)] internal static partial int g_dbus_connection_unregister_object(nint connection, uint id);
    [LibraryImport(Gio)] internal static partial void g_dbus_method_invocation_return_value(nint invocation, nint value);
    [LibraryImport(Gio, StringMarshalling = StringMarshalling.Utf8)] internal static partial void g_dbus_method_invocation_return_dbus_error(nint invocation, string name, string message);
    [LibraryImport(Gio, StringMarshalling = StringMarshalling.Utf8)] internal static partial int g_dbus_connection_emit_signal(nint connection, nint destination, string path, string @interface, string signal, nint parameters, nint* error);
    [LibraryImport(Gio)] internal static partial nint g_cancellable_new();
    [LibraryImport(Gio)] internal static partial void g_cancellable_cancel(nint cancellable);
    [LibraryImport(Glib)] internal static partial nint g_main_context_new();
    [LibraryImport(Glib)] internal static partial void g_main_context_unref(nint context);
    [LibraryImport(Glib)] internal static partial void g_main_context_push_thread_default(nint context);
    [LibraryImport(Glib)] internal static partial void g_main_context_pop_thread_default(nint context);
    [LibraryImport(Glib)] internal static partial int g_main_context_pending(nint context);
    [LibraryImport(Glib)] internal static partial int g_main_context_iteration(nint context, int mayBlock);
    [LibraryImport(Glib)] internal static partial void g_main_context_invoke_full(nint context, int priority, nint callback, nint data, nint destroy);
    [LibraryImport(Glib)] internal static partial nint g_main_loop_new(nint context, int running);
    [LibraryImport(Glib)] internal static partial void g_main_loop_run(nint loop);
    [LibraryImport(Glib)] internal static partial void g_main_loop_quit(nint loop);
    [LibraryImport(Glib)] internal static partial void g_main_loop_unref(nint loop);
    [LibraryImport(Glib)] internal static partial void g_error_free(nint error);
    [LibraryImport(Glib)] internal static partial void g_free(nint value);
    [LibraryImport(Glib, StringMarshalling = StringMarshalling.Utf8)] internal static partial void g_set_error_literal(nint* error, uint domain, int code, string message);
    [LibraryImport(Glib, StringMarshalling = StringMarshalling.Utf8)] internal static partial uint g_quark_from_string(string text);
    [LibraryImport(GObject)] internal static partial nint g_object_ref(nint value);
    [LibraryImport(GObject)] internal static partial void g_object_unref(nint value);
    [LibraryImport(GObject, StringMarshalling = StringMarshalling.Utf8)] internal static partial nuint g_signal_connect_data(nint instance, string name, nint callback, nint data, nint destroy, uint flags);
    [LibraryImport(GObject)] internal static partial void g_signal_handler_disconnect(nint instance, nuint id);
}
