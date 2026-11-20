using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Duxel.Core;
using static Duxel.Platform.Linux.GioNative;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxX11PlatformBackend : IPlatformFileDialogs
{
    IReadOnlyList<string> IPlatformFileDialogs.ShowFileDialog(UiFileDialogRequest request)
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("The XDG desktop chooser requires Linux.");
        try
        {
            return LinuxPortalFileDialogs.Show(WindowHandle, request, () =>
            {
                PollEvents();
                return !ShouldClose;
            });
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new UiNativeFileDialogUnavailableException("The native desktop chooser libraries are unavailable.");
        }
    }
}

/// <summary>Uses the desktop's XDG FileChooser portal, parented to the supplied X11 window.</summary>
[SupportedOSPlatform("linux")]
public static unsafe partial class LinuxPortalFileDialogs
{
    private const string Portal = "org.freedesktop.portal.Desktop";
    private const string Desktop = "/org/freedesktop/portal/desktop";
    private const string Chooser = "org.freedesktop.portal.FileChooser";
    private const string Request = "org.freedesktop.portal.Request";

    public static IReadOnlyList<string> Show(nuint window, UiFileDialogRequest request, Func<bool>? keepOpen = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = g_main_context_new();
        g_main_context_push_thread_default(context);
        var connection = (nint)0;
        var responseSubscription = 0u;
        var ownerSubscription = 0u;
        var response = new ResponseState();
        var handle = GCHandle.Alloc(response);
        try
        {
            nint error = 0;
            var address = g_dbus_address_get_for_bus_sync(2, 0, &error);
            CheckPortalError(ref error, "resolve the session bus", connectionUnavailable: true);
            try { connection = g_dbus_connection_new_for_address_sync(Text(address), 1 | 8, 0, 0, &error); }
            finally { g_free(address); }
            CheckPortalError(ref error, "connect to the session bus", connectionUnavailable: true);
            if (connection == 0)
                throw new UiNativeFileDialogUnavailableException("The desktop session bus connection is unavailable.");
            g_dbus_connection_set_exit_on_close(connection, 0);
            if (request.Kind == UiFileDialogKind.SelectFolder)
                RequireFolderSelection(connection);

            // Subscribe before requesting a dialog. Its response can arrive before the reply returns.
            var signal = (nint)(delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint, nint, nint, void>)&OnSignal;
            responseSubscription = Subscribe(connection, Portal, Request, "Response", null, null, 0, signal, GCHandle.ToIntPtr(handle), 0);
            ownerSubscription = Subscribe(connection, "org.freedesktop.DBus", "org.freedesktop.DBus",
                "NameOwnerChanged", "/org/freedesktop/DBus", Portal, 0, signal, GCHandle.ToIntPtr(handle), 0);
            var parent = window == 0 ? string.Empty : "x11:" + window.ToString("x", CultureInfo.InvariantCulture);
            using var args = GVariant.Tuple(GVariant.String(parent), GVariant.String(request.Title ?? DefaultTitle(request.Kind)),
                BuildOptions(request));
            var method = request.Kind == UiFileDialogKind.SaveFile ? "SaveFile" : "OpenFile";
            var reply = g_dbus_connection_call_sync(connection, Portal, Desktop, Chooser, method, args.Handle,
                0, 0, 15_000, 0, &error);
            CheckPortalError(ref error, method);
            using (var value = GVariant.Owned(reply))
            {
                if (!value.HasType("(o)"))
                    throw new IOException("The file chooser portal returned an invalid request handle.");
                using var path = value.Child(0);
                response.Path = path.AsString();
            }
            while (response.Paths is null && response.Error is null && g_dbus_connection_is_closed(connection) == 0)
            {
                while (g_main_context_pending(context) != 0 && response.Paths is null && response.Error is null)
                    g_main_context_iteration(context, 0);
                if (response.Paths is not null || response.Error is not null)
                    break;
                if (keepOpen is not null && !keepOpen())
                {
                    CloseRequest(connection, response.Path);
                    return Array.Empty<string>();
                }
                Thread.Sleep(16);
            }
            if (response.Error is not null)
                throw response.Error;
            if (response.Paths is { Count: > 1 } && !request.AllowMultiple)
                throw new IOException("The file chooser returned multiple paths for a single selection.");
            return response.Paths ?? throw new IOException("The file chooser disconnected before responding.");
        }
        finally
        {
            if (responseSubscription != 0) Unsubscribe(connection, responseSubscription);
            if (ownerSubscription != 0) Unsubscribe(connection, ownerSubscription);
            if (connection != 0)
            {
                nint error = 0;
                g_dbus_connection_close_sync(connection, 0, &error);
                if (error != 0) g_error_free(error);
                g_object_unref(connection);
            }
            g_main_context_pop_thread_default(context);
            g_main_context_unref(context);
            handle.Free();
        }
    }

    private static GVariant BuildOptions(UiFileDialogRequest request)
    {
        var options = new List<GVariant>();
        try
        {
            Add("handle_token", GVariant.String("duxel_" + Guid.NewGuid().ToString("N")));
            Add("modal", GVariant.Bool(true));
            if (request.AllowMultiple) Add("multiple", GVariant.Bool(true));
            if (request.Kind == UiFileDialogKind.SelectFolder) Add("directory", GVariant.Bool(true));
            if (request.InitialDirectory is { } directory)
                Add("current_folder", GVariant.Bytes(Encoding.UTF8.GetBytes(directory + '\0')));
            if (request.SuggestedFileName is { } name)
            {
                if (request.DefaultExtension is { } extension && !Path.HasExtension(name))
                    name += "." + extension;
                Add("current_name", GVariant.String(name));
            }
            if (request.Filters.Count != 0)
                Add("filters", GVariant.Array("(sa(us))", request.Filters.Select(filter => GVariant.Tuple(
                    GVariant.String(filter.Name), GVariant.Array("(us)", filter.Patterns.Select(pattern =>
                        GVariant.Tuple(GVariant.UInt(0), GVariant.String(pattern))).ToArray()))).ToArray()));
            else if (request.DefaultExtension is { } defaultExtension)
                Add("filters", GVariant.Array("(sa(us))", GVariant.Tuple(GVariant.String(defaultExtension),
                    GVariant.Array("(us)", GVariant.Tuple(GVariant.UInt(0), GVariant.String("*." + defaultExtension))))));
            return GVariant.Array("{sv}", options.ToArray());
        }
        finally
        {
            foreach (var option in options)
                option.Dispose();
        }

        void Add(string key, GVariant value) => options.Add(GVariant.DictEntry(GVariant.String(key), GVariant.Box(value)));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnSignal(nint connection, nint sender, nint path, nint @interface, nint signal, nint args, nint userData)
    {
        var response = (ResponseState)GCHandle.FromIntPtr(userData).Target!;
        if (response.Paths is not null || response.Error is not null)
            return;
        try
        {
            using var value = GVariant.Referenced(args);
            if (Text(signal) == "NameOwnerChanged")
            {
                using var owner = value.Child(2);
                if (owner.AsString().Length == 0)
                    response.Error = new IOException("The desktop file chooser closed unexpectedly.");
                return;
            }
            if (Text(path) != response.Path)
                return;
            if (!value.HasType("(ua{sv})"))
                throw new IOException("The file chooser portal returned an invalid response.");
            using var code = value.Child(0);
            if (code.AsUInt() == 1)
            {
                response.Paths = Array.Empty<string>();
                return;
            }
            if (code.AsUInt() != 0)
                throw new IOException($"The file chooser did not complete the request (response {code.AsUInt()}).");
            using var results = value.Child(1);
            using var uris = results.Lookup("uris", "as") ?? throw new IOException("The file chooser did not return any paths.");
            var paths = new string[uris.ChildCount];
            for (var i = 0; i < paths.Length; i++)
            {
                using var uriText = uris.Child(i);
                if (!Uri.TryCreate(uriText.AsString(), UriKind.Absolute, out var uri) || !uri.IsFile || !uri.IsLoopback
                    || !Path.IsPathFullyQualified(uri.LocalPath))
                    throw new IOException("The file chooser returned a non-local file URI.");
                paths[i] = uri.LocalPath;
            }
            response.Paths = Array.AsReadOnly(paths);
        }
        catch (Exception error) { response.Error = error; }
    }

    private static string DefaultTitle(UiFileDialogKind kind) => kind switch
    {
        UiFileDialogKind.SaveFile => "Save file",
        UiFileDialogKind.SelectFolder => "Select folder",
        _ => "Open file"
    };

    private static void RequireFolderSelection(nint connection)
    {
        nint error = 0;
        using var args = GVariant.Tuple(GVariant.String(Chooser), GVariant.String("version"));
        var result = g_dbus_connection_call_sync(connection, Portal, Desktop, "org.freedesktop.DBus.Properties",
            "Get", args.Handle, 0, 0, 15_000, 0, &error);
        CheckPortalError(ref error, "read the folder picker version");
        using var reply = GVariant.Owned(result);
        if (!reply.HasType("(v)"))
            throw new IOException("The file chooser returned invalid version information.");
        using var boxed = reply.Child(0);
        using var version = boxed.Unbox();
        if (!version.HasType("u") || version.AsUInt() < 3)
            throw new UiNativeFileDialogUnavailableException("The installed desktop FileChooser cannot choose folders.");
    }

    private static void CloseRequest(nint connection, string path)
    {
        nint error = 0;
        var result = g_dbus_connection_call_sync(connection, Portal, path, Request, "Close", 0, 0, 0, 1000, 0, &error);
        if (result != 0) GVariant.Owned(result).Dispose();
        if (error != 0) g_error_free(error);
    }

    private static void CheckPortalError(ref nint error, string operation, bool connectionUnavailable = false)
    {
        if (error == 0)
            return;
        var value = *(Error*)error;
        var message = Text(value.Message);
        var remoteName = RemoteError(error);
        var code = Marshal.PtrToStringUTF8(remoteName);
        if (remoteName != 0) g_free(remoteName);
        g_error_free(error);
        error = 0;
        if (connectionUnavailable || code is "org.freedesktop.DBus.Error.ServiceUnknown"
            or "org.freedesktop.DBus.Error.NameHasNoOwner" or "org.freedesktop.DBus.Error.UnknownMethod"
            or "org.freedesktop.DBus.Error.UnknownInterface" or "org.freedesktop.DBus.Error.UnknownObject"
            or "org.freedesktop.DBus.Error.NotSupported")
            throw new UiNativeFileDialogUnavailableException($"Desktop file chooser could not {operation}: {message}");
        throw new IOException($"Desktop file chooser could not {operation}: {message}");
    }

    private sealed class ResponseState
    {
        internal string Path = string.Empty;
        internal IReadOnlyList<string>? Paths;
        internal Exception? Error;
    }

    [LibraryImport("libgio-2.0.so.0", EntryPoint = "g_dbus_connection_signal_subscribe", StringMarshalling = StringMarshalling.Utf8)]
    private static partial uint Subscribe(nint connection, string sender, string @interface, string member, string? path,
        string? argument, uint flags, nint callback, nint data, nint destroy);

    [LibraryImport("libgio-2.0.so.0", EntryPoint = "g_dbus_connection_signal_unsubscribe")]
    private static partial void Unsubscribe(nint connection, uint subscription);

    [LibraryImport("libgio-2.0.so.0", EntryPoint = "g_dbus_error_get_remote_error")]
    private static partial nint RemoteError(nint error);
}
