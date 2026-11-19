using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static unsafe partial class AtSpiProbe
{
    internal static void RunComponentScroll(uint parentPid, string output)
    {
        if (File.Exists(output))
            throw new IOException("Use a fresh component result path.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent component AT-SPI initialization failed.");
        atspi_set_timeout(2000, 2000);
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }
        try
        {
            using var app = OwnedApplication(parentPid);
            using var window = Child(app.Value, 0);
            using var target = Find(window.Value, value => Name(value) == ComponentScrollTests.Scene.TargetName)
                ?? throw new InvalidOperationException("The owned native component target is missing.");
            using var component = new NativeObject(atspi_accessible_get_component_iface(target.Value));
            using (var state = new NativeObject(atspi_accessible_get_state_set(target.Value)))
                Check(atspi_state_set_contains(state.Value, 25) == 0, "The independent client finds an actually offscreen component");
            foreach (uint placement in new uint[] { 0, 1, 2, 3, 4, 5, 6 })
            {
                Check(ScrollComponentTo(component.Value, placement), "Native component placement completes, " + placement);
                var box = ComponentBox(target.Value);
                Check(box.Width == 120 && box.Height == 32 && box.X >= 10 && box.Y >= 10
                    && box.X + box.Width <= 250 && box.Y + box.Height <= 170,
                    "The whole original button is really visible after native placement, " + placement);
            }
            foreach (uint coordinates in new uint[] { 0, 1, 2 })
            {
                var windowBox = ComponentBox(target.Value);
                var requested = ComponentBox(target.Value, coordinates);
                Check(ScrollComponentPoint(component.Value, coordinates, 80 + requested.X - windowBox.X, 70 + requested.Y - windowBox.Y),
                    "Native component-to-point accepts its declared coordinate system, " + coordinates);
                var aligned = ComponentBox(target.Value);
                Check(Math.Abs(aligned.X - 80) <= 1 && Math.Abs(aligned.Y - 70) <= 1 && aligned.Width == 120 && aligned.Height == 32,
                    "Screen/window/parent conversion reaches the requested client point, " + coordinates);
            }
            var before = ComponentBox(target.Value);
            Check(!ScrollComponentTo(component.Value, 7) && !ScrollComponentPoint(component.Value, 1, 300, 250)
                && ComponentBox(target.Value).Equals(before), "Invalid placement and out-of-viewport points fail without moving the component");
            using (var state = new NativeObject(atspi_accessible_get_state_set(target.Value)))
                Check(atspi_state_set_contains(state.Value, 12) == 0 && Name(target.Value) == ComponentScrollTests.Scene.TargetName,
                    "Component scrolling preserves the original control and does not grab keyboard focus");
            using var windowComponent = new NativeObject(atspi_accessible_get_component_iface(window.Value));
            Check(!ScrollComponentTo(windowComponent.Value, 6), "The native window cannot scroll an unrelated logical ancestor");
        }
        finally { atspi_exit(); }
        File.WriteAllText(output, JsonSerializer.Serialize(new NativeComponentScrollResult(
            RuntimeInformation.FrameworkDescription, checks.Count, checks.ToArray()), ComponentScrollJson.Default.NativeComponentScrollResult));
    }

    private static bool ScrollComponentTo(nint component, uint placement)
    {
        nint error = 0;
        var result = atspi_component_scroll_to(component, placement, &error);
        Error(ref error);
        return result != 0;
    }

    private static bool ScrollComponentPoint(nint component, uint coordinates, int x, int y)
    {
        nint error = 0;
        var result = atspi_component_scroll_to_point(component, coordinates, x, y, &error);
        Error(ref error);
        return result != 0;
    }

    [LibraryImport(Library)] private static partial int atspi_component_scroll_to(nint component, uint placement, nint* error);
    [LibraryImport(Library)] private static partial int atspi_component_scroll_to_point(nint component, uint coordinates, int x, int y, nint* error);
}

internal sealed record NativeComponentScrollResult(string framework, int passed, string[] checks);
[JsonSerializable(typeof(NativeComponentScrollResult))]
internal partial class ComponentScrollJson : JsonSerializerContext;
