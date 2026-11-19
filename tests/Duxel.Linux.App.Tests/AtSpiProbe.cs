using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

// Independent native AT-SPI/libdbus client, executed in a separate owned process.
internal static unsafe partial class AtSpiProbe
{
    private const string Library = "libatspi.so.0";
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeError
    {
        internal uint Domain; internal int Code; internal nint Message;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        internal int X, Y, Width, Height;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X, Y;
    }
    private sealed class NativeObject : SafeHandle
    {
        internal NativeObject(nint value) : base(0, true)
        {
            if (value == 0)
                throw new InvalidOperationException("The independent AT-SPI client received a null object.");
            SetHandle(value);
        }
        public override bool IsInvalid => handle == 0;
        protected override bool ReleaseHandle()
        {
            g_object_unref(handle);
            return true;
        }
        internal nint Value => handle;
    }
    private static void Error(ref nint error)
    {
        if (error == 0)
            return;
        var native = *(NativeError*)error;
        var message = Marshal.PtrToStringUTF8(native.Message);
        g_error_free(error);
        error = 0;
        throw new InvalidOperationException("Independent AT-SPI call failed: " + message);
    }
    private static string Name(nint value)
    {
        nint error = 0;
        var text = atspi_accessible_get_name(value, &error);
        try
        {
            Error(ref error);
            return Marshal.PtrToStringUTF8(text) ?? throw new InvalidDataException("No accessible name.");
        }
        finally { if (text != 0) g_free(text); }
    }
    private static int Count(nint value)
    {
        nint error = 0;
        var count = atspi_accessible_get_child_count(value, &error);
        Error(ref error);
        return count;
    }
    private static NativeObject Child(nint value, int index)
    {
        nint error = 0;
        var child = atspi_accessible_get_child_at_index(value, index, &error);
        Error(ref error);
        return new(child);
    }
    private static int Role(nint value)
    {
        nint error = 0;
        var role = atspi_accessible_get_role(value, &error);
        Error(ref error);
        return role;
    }
    private static void Pump()
    {
        while (g_main_context_pending(0) != 0)
            g_main_context_iteration(0, 0);
    }
    private static void Wait(Func<bool> condition, string phase)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > 5000)
                throw new TimeoutException("Independent AT-SPI client: " + phase);
            Pump();
            Thread.Sleep(10);
        }
    }
    private static NativeObject? Find(nint value, Func<nint, bool> predicate)
    {
        if (predicate(value))
            return new(g_object_ref(value));
        var count = Count(value);
        for (var i = 0; i < count; i++)
        {
            using var child = Child(value, i);
            var found = Find(child.Value, predicate);
            if (found is not null)
                return found;
        }
        return null;
    }

    internal static void Run(uint parentPid, string output, nuint nativeWindow, string privateSelection)
    {
        if (File.Exists(output))
            throw new IOException("AT-SPI probe output already exists.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent AT-SPI initialization failed.");
        atspi_set_timeout(2000, 2000);
        var checks = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            checks.Add(message);
            Console.WriteLine("PASS " + message);
        }
        try
        {
            using var app = OwnedApplication(parentPid);
            using var events = new EventWatch();
            atspi_accessible_set_cache_mask(app.Value, 127);
            Check(Role(app.Value) == 75, "The registry exposes an application role for the owned process");
            Check(Name(app.Value) == "Duxel owned Linux application fixture", "The native application name preserves its exact title");
            Check(Count(app.Value) == 1, "The application has exactly one native window accessible");
            using var window = Child(app.Value, 0);
            Check(Role(window.Value) == 23, "The application child exposes a frame role");
            using var button = Find(window.Value, value => Role(value) == 43 && Name(value).StartsWith("클릭 수 :", StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The owned counter button was not exported.");
            using var action = new NativeObject(atspi_accessible_get_action_iface(button.Value));
            events.Button = button.Value;
            Check(Name(button.Value) == "클릭 수 : 1", "The native client caches the original Korean name before invocation");
            nint callError = 0;
            var actionCount = atspi_action_get_n_actions(action.Value, &callError);
            Error(ref callError);
            Check(actionCount == 1, "The native button has exactly one default action");
            var actionName = atspi_action_get_action_name(action.Value, 0, &callError);
            try
            {
                Error(ref callError);
                Check(Marshal.PtrToStringUTF8(actionName) == "click", "The action name follows the native AT-SPI contract");
            }
            finally { if (actionName != 0) g_free(actionName); }
            using var component = new NativeObject(atspi_accessible_get_component_iface(button.Value));
            var rectangle = atspi_component_get_extents(component.Value, 1, &callError);
            try
            {
                Error(ref callError);
                if (rectangle == 0)
                    throw new InvalidDataException("The native component returned no extents.");
                var r = *(Rect*)rectangle;
                Check(r.X == 20 && r.Y == 70 && r.Width == 200 && r.Height == 36, "Native window coordinates match the actual counter button");
            }
            finally { if (rectangle != 0) g_free(rectangle); }
            var inside = atspi_component_contains(component.Value, 219, 105, 1, &callError);
            Error(ref callError);
            var outside = atspi_component_contains(component.Value, 220, 106, 1, &callError);
            Error(ref callError);
            Check(inside != 0 && outside == 0, "Native component bounds include their interior and exclude the right and bottom edges");
            using var windowComponent = new NativeObject(atspi_accessible_get_component_iface(window.Value));
            var hitPointer = atspi_component_get_accessible_at_point(windowComponent.Value, 40, 85, 1, &callError);
            Error(ref callError);
            using var hit = new NativeObject(hitPointer);
            Check(hit.Value == button.Value, "Independent native hit testing resolves the actual counter button");
            var invoked = atspi_action_do_action(action.Value, 0, &callError);
            Error(ref callError);
            Check(invoked != 0, "An external native action reaches the render-thread action queue");
            Wait(() => { events.Check(); return events.Names > 0 && Name(button.Value) == "클릭 수 : 2"; }, "the cached name's actual property-change event");
            Check(true, "A native property-change event updates the cached Korean name without clearing the cache");
            VerifyRange(window.Value, events, Check);
            VerifyProgress(window.Value, Check);
            VerifySelection(window.Value, events, Check);
            using var input = Find(window.Value, value => Role(value) == 79 && Name(value) == "입력")
                ?? throw new InvalidOperationException("The owned input field was not exported.");
            using var inputComponent = new NativeObject(atspi_accessible_get_component_iface(input.Value));
            var inputBounds = atspi_component_get_extents(inputComponent.Value, 1, &callError);
            Error(ref callError);
            try
            {
                var inputSize = atspi_component_get_size(inputComponent.Value, &callError);
                try
                {
                    Error(ref callError);
                    if (inputBounds == 0 || inputSize == 0)
                        throw new InvalidDataException("The native input returned no geometry.");
                    var bounds = *(Rect*)inputBounds;
                    var size = *(Point*)inputSize;
                    Check(bounds.Width > 0 && bounds.Height > 0 && bounds.Width == size.X && bounds.Height == size.Y,
                        "Native input extents and size use the same enclosing pixel rectangle");
                }
                finally { if (inputSize != 0) g_free(inputSize); }
            }
            finally { if (inputBounds != 0) g_free(inputBounds); }
            events.Input = input.Value;
            using (var before = new NativeObject(atspi_accessible_get_state_set(input.Value)))
                Check(atspi_state_set_contains(before.Value, 12) == 0, "The native client caches the unfocused input state before requesting focus");
            var focused = atspi_component_grab_focus(inputComponent.Value, &callError);
            Error(ref callError);
            Check(focused != 0, "An external native component can focus the shared input widget");
            Wait(() =>
            {
                events.Check();
                using var states = new NativeObject(atspi_accessible_get_state_set(input.Value));
                return events.Focus > 0 && atspi_state_set_contains(states.Value, 12) != 0;
            }, "the published focused state");
            Check(true, "A native state-change event updates the cached input focus without clearing the cache");
            VerifyText(input.Value, button.Value, nativeWindow, events, Check);
            VerifyEditableText(input.Value, button.Value, nativeWindow, privateSelection, Check);
            VerifyPaintEvents(input.Value, events, output, Check);
            nint parentError = 0;
            using var buttonParent = new NativeObject(atspi_accessible_get_parent(button.Value, &parentError));
            Error(ref parentError);
            Check(Count(buttonParent.Value) > 0, "The native child has a live navigable parent");
        }
        finally { atspi_exit(); }
        File.WriteAllText(output, JsonSerializer.Serialize(new LinuxCleanupResult(RuntimeInformation.FrameworkDescription, parentPid, true, checks.Count, checks), LinuxProbeResultsJson.Default.LinuxCleanupResult));
    }

    internal static void AssertAbsent(uint parentPid, string output)
    {
        if (File.Exists(output))
            throw new IOException("AT-SPI retirement output already exists.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent AT-SPI initialization failed.");
        atspi_set_timeout(2000, 2000);
        try
        {
            using var desktop = new NativeObject(atspi_get_desktop(0));
            Wait(() =>
            {
                atspi_accessible_clear_cache(desktop.Value);
                for (var i = 0; i < Count(desktop.Value); i++)
                {
                    using var candidate = Child(desktop.Value, i);
                    nint error = 0;
                    var pid = atspi_accessible_get_process_id(candidate.Value, &error);
                    Error(ref error);
                    if (pid == parentPid)
                        return false;
                }
                return true;
            }, "the owned application is retired from the registry");
            File.WriteAllText(output, JsonSerializer.Serialize(new LinuxRegistryResult(RuntimeInformation.FrameworkDescription, parentPid, false), LinuxProbeResultsJson.Default.LinuxRegistryResult));
        }
        finally { atspi_exit(); }
    }

    [LibraryImport(Library)] private static partial int atspi_init();
    [LibraryImport(Library)] private static partial int atspi_exit();
    [LibraryImport(Library)] private static partial void atspi_set_timeout(int timeout, int startup);
    [LibraryImport(Library)] private static partial nint atspi_get_desktop(int index);
    [LibraryImport(Library)] private static partial uint atspi_accessible_get_process_id(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_name(nint value, nint* error);
    [LibraryImport(Library)] private static partial int atspi_accessible_get_role(nint value, nint* error);
    [LibraryImport(Library)] private static partial int atspi_accessible_get_child_count(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_child_at_index(nint value, int index, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_parent(nint value, nint* error);
    [LibraryImport(Library)] private static partial void atspi_accessible_clear_cache(nint value);
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_action_iface(nint value);
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_component_iface(nint value);
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_value_iface(nint value);
    [LibraryImport(Library)] private static partial int atspi_action_get_n_actions(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_action_get_action_name(nint value, int index, nint* error);
    [LibraryImport(Library)] private static partial int atspi_action_do_action(nint value, int index, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_component_get_extents(nint value, uint coordinates, nint* error);
    [LibraryImport(Library)] private static partial int atspi_component_contains(nint value, int x, int y, uint coordinates, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_component_get_accessible_at_point(nint value, int x, int y, uint coordinates, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_component_get_size(nint value, nint* error);
    [LibraryImport(Library)] private static partial int atspi_component_grab_focus(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_accessible_get_state_set(nint value);
    [LibraryImport(Library)] private static partial int atspi_state_set_contains(nint value, int state);
    [LibraryImport("libgobject-2.0.so.0")] private static partial nint g_object_ref(nint value);
    [LibraryImport("libgobject-2.0.so.0")] private static partial void g_object_unref(nint value);
    [LibraryImport("libglib-2.0.so.0")] private static partial void g_free(nint value);
    [LibraryImport("libglib-2.0.so.0")] private static partial void g_error_free(nint value);
    [LibraryImport("libglib-2.0.so.0")] private static partial int g_main_context_pending(nint context);
    [LibraryImport("libglib-2.0.so.0")] private static partial int g_main_context_iteration(nint context, int mayBlock);
}
