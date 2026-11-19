using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static unsafe partial class AtSpiProbe
{
    internal static void RunImageComponent(uint parentPid, string output, bool nested, bool rtl)
    {
        if (File.Exists(output))
            throw new IOException("Use a fresh image Component result path.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent image Component AT-SPI initialization failed.");
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
            using var document = Find(window.Value, value => Role(value) == 79 && Name(value) == MarkdownImageComponentScrollTests.Scene.DocumentName)
                ?? throw new InvalidOperationException("The original image Component document is missing.");
            using var text = new NativeObject(atspi_accessible_get_text_iface(document.Value));
            var source = ReadText(text.Value, 0, -1);
            var caret = TextCaret(text.Value);
            var selections = TextSelectionCount(text.Value);
            using var first = Find(document.Value, value => Role(value) == 27 && Name(value) == "target")
                ?? throw new InvalidOperationException("The initially offscreen image is missing.");
            using (var state = new NativeObject(atspi_accessible_get_state_set(first.Value)))
                Check(atspi_state_set_contains(state.Value, 25) == 0, "Native image Component starts outside its actual viewport");
            foreach (var name in new[] { "target", "linked", "table" })
            {
                using var image = Find(document.Value, value => Role(value) == 27 && Name(value) == name)
                    ?? throw new InvalidOperationException("The native image Component is missing: " + name);
                using var component = new NativeObject(atspi_accessible_get_component_iface(image.Value));
                foreach (uint placement in new uint[] { 0, 1, 2, 3, 4, 5, 6 })
                {
                    Check(ScrollComponentTo(component.Value, placement), "Native image Component accepts placement, " + name + "/" + placement);
                    var box = ComponentBox(image.Value);
                    var left = nested ? rtl ? 22 : 10 : 22;
                    var top = nested ? 10 : 20;
                    var right = nested ? rtl ? 270 : 258 : 398;
                    var bottom = nested ? 198 : 260;
                    Check(box.Width is >= 120 and <= 121 && box.Height is >= 60 and <= 61 && box.X >= left - 1 && box.Y >= top - 1
                        && box.X + box.Width <= right + 1 && box.Y + box.Height <= bottom + 1,
                        $"Actual native image bounds are complete and visible, {name}/{placement}; box={box.X},{box.Y},{box.Width},{box.Height}");
                }
                foreach (uint coordinates in new uint[] { 0, 1, 2 })
                {
                    var local = ComponentBox(image.Value);
                    var requested = ComponentBox(image.Value, coordinates);
                    Check(ScrollComponentPoint(component.Value, coordinates, 80 + requested.X - local.X, 70 + requested.Y - local.Y),
                        "Native image Component-to-point accepts its declared coordinate system, " + name + "/" + coordinates);
                    var aligned = ComponentBox(image.Value);
                    Check(Math.Abs(aligned.X - 80) <= 1 && Math.Abs(aligned.Y - 70) <= 1 && aligned.Width is >= 120 and <= 121 && aligned.Height is >= 60 and <= 61,
                        "Actual image reaches the requested client point through screen/window/parent coordinates, " + name + "/" + coordinates);
                }
                var before = ComponentBox(image.Value);
                Check(!ScrollComponentPoint(component.Value, 1, 600, 450) && ComponentBox(image.Value).Equals(before),
                    "Invalid native image target fails without moving the original geometry, " + name);
                Check(Name(image.Value) == name && ReadText(text.Value, 0, -1) == source && TextCaret(text.Value) == caret && TextSelectionCount(text.Value) == selections,
                    "Native image Component scrolling preserves original Unicode source, caret, selection and image, " + name);
            }
            using (var state = new NativeObject(atspi_accessible_get_state_set(document.Value)))
                Check(atspi_state_set_contains(state.Value, 12) == 0 && source == MarkdownImageComponentScrollTests.Scene.Source,
                    "Native image reveal does not focus its document or replace the source");
        }
        finally { atspi_exit(); }
        File.WriteAllText(output, JsonSerializer.Serialize(new NativeImageComponentResult(
            RuntimeInformation.FrameworkDescription, nested, rtl, checks.Count, checks.ToArray()), ImageComponentJson.Default.NativeImageComponentResult));
    }
}

internal sealed record NativeImageComponentResult(string framework, bool nested, bool rtl, int passed, string[] checks);
[JsonSerializable(typeof(NativeImageComponentResult))]
internal partial class ImageComponentJson : JsonSerializerContext;
