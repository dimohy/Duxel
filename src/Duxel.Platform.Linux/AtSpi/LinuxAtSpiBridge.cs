using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Duxel.Core;
using static Duxel.Platform.Linux.GioNative;

namespace Duxel.Platform.Linux;

public sealed record LinuxAtSpiOptions
{
    public bool Enabled { get; init; } = true;
    public int CallTimeoutMilliseconds { get; init; } = 5000;
    internal void Validate()
    {
        if (CallTimeoutMilliseconds is <= 0 or > 60000)
            throw new ArgumentOutOfRangeException(nameof(CallTimeoutMilliseconds));
    }
}

internal sealed record AtSpiWindowGeometry(int X, int Y, int Width, int Height, bool Visible, bool Focused, float ContentScale = 1);

/// <summary>Owns a private AT-SPI bus connection and dispatch context. Native callbacks never enter Xlib.</summary>
internal sealed unsafe partial class LinuxAtSpiBridge : IDisposable
{
    private const string RootPath = "/org/a11y/atspi/accessible/root", WindowPath = RootPath + "/window";
    private const string NullPath = "/org/a11y/atspi/null", CachePath = "/org/a11y/atspi/cache";
    private const string Accessible = "org.a11y.atspi.Accessible", Application = "org.a11y.atspi.Application";
    private const string Component = "org.a11y.atspi.Component", ActionInterface = "org.a11y.atspi.Action", Cache = "org.a11y.atspi.Cache";
    private const string ValueInterface = "org.a11y.atspi.Value";
    private const string SelectionInterface = "org.a11y.atspi.Selection";
    private const string TextInterface = "org.a11y.atspi.Text";
    private const string EditableTextInterface = "org.a11y.atspi.EditableText";
    private const string ImageInterface = "org.a11y.atspi.Image";
    private readonly UiAccessibilityTree _tree;
    private readonly string _title;
    private readonly LinuxAtSpiOptions _options;
    private readonly Func<AtSpiWindowGeometry> _geometry;
    private readonly Action _wakeOwner;
    private readonly Func<bool> _focusWindow;
    private readonly object _gate = new();
    private readonly Thread _thread;
    private readonly TaskCompletionSource _initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<string, uint[]> _exports = new(StringComparer.Ordinal);
    private nint _context, _loop, _connection, _info, _cancellable;
    private GCHandle _self;
    private ExceptionDispatchInfo? _failure;
    private volatile bool _stopping;
    private bool _disposed;
    private nuint _closedHandler;
    private int _refreshQueued, _applicationId;
    private sealed record Reference(string Bus, string Path);
    private Reference _parent = new("org.a11y.atspi.Registry", RootPath);
    internal string BusName { get; private set; } = "";

    internal LinuxAtSpiBridge(UiAccessibilityTree tree, string title, LinuxAtSpiOptions options,
        Func<AtSpiWindowGeometry> geometry, Action wakeOwner, Func<bool> focusWindow)
    {
        options.Validate();
        (_tree, _title, _options, _geometry, _wakeOwner) = (tree, title, options, geometry, wakeOwner);
        _focusWindow = focusWindow;
        _cancellable = g_cancellable_new();
        _self = GCHandle.Alloc(this);
        _thread = new Thread(Serve) { IsBackground = true, Name = "DuxelAT-SPI" };
        _thread.Start();
        try
        {
            _initialized.Task.WaitAsync(TimeSpan.FromMilliseconds(options.CallTimeoutMilliseconds)).GetAwaiter().GetResult();
            nint connection;
            lock (_gate)
            {
                ThrowIfFaulted();
                connection = g_object_ref(_connection);
            }
            try
            {
                // Registry callbacks must be dispatchable while Embed waits for its reply.
                using var request = GVariant.Tuple(ObjectReference(RootPath));
                using var reply = Call(connection, "org.a11y.atspi.Registry", RootPath, "org.a11y.atspi.Socket", "Embed", request);
                using var parent = reply.Child(0);
                using var bus = parent.Child(0);
                using var path = parent.Child(1);
                Volatile.Write(ref _parent, new(bus.AsString(), path.AsString()));
            }
            finally { g_object_unref(connection); }
            _tree.Published += RequestRefresh;
            _tree.Announcement += QueueAnnouncement;
            RequestRefresh();
        }
        catch { Dispose(); throw; }
    }

    private GVariant Call(nint connection, string destination, string path, string @interface, string method, GVariant? parameters = null)
    {
        nint error = 0;
        var result = g_dbus_connection_call_sync(connection, destination, path, @interface, method,
            parameters?.Handle ?? 0, 0, 0, _options.CallTimeoutMilliseconds, _cancellable, &error);
        CheckError(ref error, method);
        return GVariant.Owned(result);
    }

    private void Serve()
    {
        try
        {
            LinuxTextBoundaries.EnsureAvailable();
            _context = g_main_context_new();
            g_main_context_push_thread_default(_context);
            _loop = g_main_loop_new(_context, 0);
            nint error = 0;
            var sessionAddress = g_dbus_address_get_for_bus_sync(2, _cancellable, &error);
            CheckError(ref error, "resolve session bus");
            nint session;
            try
            {
                session = g_dbus_connection_new_for_address_sync(Text(sessionAddress), 1 | 8, 0, _cancellable, &error);
            }
            finally { g_free(sessionAddress); }
            CheckError(ref error, "connect session bus");
            if (session == 0)
                throw new IOException("The session bus connection is unavailable.");
            g_dbus_connection_set_exit_on_close(session, 0);
            string address;
            try
            {
                using var addressReply = Call(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus", "GetAddress");
                using var value = addressReply.Child(0);
                address = value.AsString();
            }
            finally
            {
                nint closeError = 0;
                g_dbus_connection_close_sync(session, 0, &closeError);
                try
                {
                    CheckError(ref closeError, "close session lookup");
                }
                finally { g_object_unref(session); }
            }
            _connection = g_dbus_connection_new_for_address_sync(address, 1 | 8, 0, _cancellable, &error);
            CheckError(ref error, "connect accessibility bus");
            if (_connection == 0)
                throw new IOException("The AT-SPI bus connection is unavailable.");
            g_dbus_connection_set_exit_on_close(_connection, 0);
            BusName = Text(g_dbus_connection_get_unique_name(_connection));
            _closedHandler = g_signal_connect_data(_connection, "closed", (nint)(delegate* unmanaged[Cdecl]<nint, int, nint, nint, void>)&ConnectionClosed,
                GCHandle.ToIntPtr(_self), 0, 0);
            if (_closedHandler == 0)
                throw new IOException("The AT-SPI disconnect observer could not be registered.");
            _info = g_dbus_node_info_new_for_xml(AtSpiSchema.Xml, &error);
            CheckError(ref error, "parse introspection");
            Refresh();
            _initialized.SetResult();
            g_main_loop_run(_loop);
        }
        catch (Exception error)
        {
            Fail(error);
            _initialized.TrySetException(error);
        }
        finally
        {
            nint context, loop, connection;
            lock (_gate)
            {
                _stopping = true;
                (context, loop, connection) = (_context, _loop, _connection);
                _context = _loop = _connection = 0;
            }
            foreach (var registrations in _exports.Values)
                foreach (var registration in registrations)
                    g_dbus_connection_unregister_object(connection, registration);
            _exports.Clear();
            _nodes.Clear();
            if (connection != 0)
            {
                if (_closedHandler != 0)
                    g_signal_handler_disconnect(connection, _closedHandler);
                nint error = 0;
                if (g_dbus_connection_is_closed(connection) == 0)
                    g_dbus_connection_close_sync(connection, 0, &error);
                if (error != 0)
                {
                    try
                    {
                        CheckError(ref error, "close accessibility bus");
                    }
                    catch (Exception closeError) { lock (_gate) _failure ??= ExceptionDispatchInfo.Capture(closeError); }
                }
                g_object_unref(connection);
            }
            if (_info != 0)
                g_dbus_node_info_unref(_info);
            if (loop != 0)
                g_main_loop_unref(loop);
            if (context != 0)
            {
                while (g_main_context_pending(context) != 0)
                    g_main_context_iteration(context, 0);
                g_main_context_pop_thread_default(context);
                g_main_context_unref(context);
            }
        }
    }

    private void Fail(Exception error)
    {
        lock (_gate)
        {
            if (_stopping)
                return;
            _failure ??= ExceptionDispatchInfo.Capture(error);
            if (_loop != 0)
                g_main_loop_quit(_loop);
            try
            {
                _wakeOwner();
            }
            catch (Exception wakeError) { _failure = ExceptionDispatchInfo.Capture(new AggregateException(error, wakeError)); }
        }
    }

    internal void ThrowIfFaulted()
    {
        lock (_gate)
            _failure?.Throw();
    }

    internal void RequestRefresh()
    {
        lock (_gate)
        {
            if (_stopping || Interlocked.Exchange(ref _refreshQueued, 1) != 0)
                return;
            g_main_context_invoke_full(_context, 0, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&DispatchRefresh, GCHandle.ToIntPtr(_self), 0);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int DispatchRefresh(nint data)
    {
        var owner = (LinuxAtSpiBridge)GCHandle.FromIntPtr(data).Target!;
        Interlocked.Exchange(ref owner._refreshQueued, 0);
        try
        {
            if (!owner._stopping)
                owner.Refresh();
        }
        catch (Exception error) { owner.Fail(error); }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ConnectionClosed(nint connection, int vanished, nint error, nint data)
    {
        var owner = (LinuxAtSpiBridge)GCHandle.FromIntPtr(data).Target!;
        try
        {
            owner.Fail(new IOException("The AT-SPI bus connection closed."));
        }
        catch (Exception failure) { owner.Fail(failure); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = _stopping = true;
            _tree.Published -= RequestRefresh;
            _tree.Announcement -= QueueAnnouncement;
            _pendingAnnouncements = null;
            g_cancellable_cancel(_cancellable);
            if (_context != 0)
                g_main_context_invoke_full(_context, 0, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&Quit, GCHandle.ToIntPtr(_self), 0);
        }
        _thread.Join();
        _self.Free();
        g_object_unref(_cancellable);
        _cancellable = 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Quit(nint data)
    {
        var owner = (LinuxAtSpiBridge)GCHandle.FromIntPtr(data).Target!;
        if (owner._loop != 0)
            g_main_loop_quit(owner._loop);
        return 0;
    }
}
