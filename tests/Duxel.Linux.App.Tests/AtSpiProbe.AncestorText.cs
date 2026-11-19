using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

internal static unsafe partial class AtSpiProbe
{
    internal static void RunAncestor(uint parentPid, string output, bool rightToLeft = false, bool virtualized = false,
        bool variableExtents = false, string? popupMode = null)
    {
        if (File.Exists(output))
            throw new IOException("The owned ancestor result already exists.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent ancestor AT-SPI initialization failed.");
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
            using var window = Child(app.Value, 0);
            var fields = new List<NativeObject>();
            void Collect(nint root)
            {
                for (var index = 0; index < Count(root); index++)
                {
                    using var child = Child(root, index);
                    var name = Name(child.Value);
                    var target = virtualized ? name is "Virtual single 123456790" or "Virtual multiline 123456790" or "Virtual markdown 123456790"
                        : name == "Nested text";
                    if (Role(child.Value) == 79 && target)
                        fields.Add(new(g_object_ref(child.Value)));
                    else
                        Collect(child.Value);
                }
            }
            Collect(window.Value);
            try
            {
                Check(fields.Count == (popupMode is null ? 3 : 1), "The independent client finds the original offscreen text producers");
                for (var index = 0; index < fields.Count; index++)
                {
                    var field = fields[index].Value;
                    using var text = new NativeObject(atspi_accessible_get_text_iface(field));
                    var source = ReadText(text.Value, 0, -1);
                    var target = popupMode is null ? index == 0 ? 30 : 8 * 63 + 20 : popupMode == "single" ? 30 : 8 * 63 + 20;
                    var caret = TextCaret(text.Value);
                    var selections = TextSelectionCount(text.Value);
                    var left = 10 + index * 210;
                    var top = 10;
                    var width = 190;
                    var height = 180;
                    if (popupMode is not null)
                    {
                        nint error = 0;
                        using var parent = new NativeObject(atspi_accessible_get_parent(field, &error));
                        Error(ref error);
                        using var component = new NativeObject(atspi_accessible_get_component_iface(parent.Value));
                        var pointer = atspi_component_get_extents(component.Value, 1, &error);
                        try
                        {
                            Error(ref error);
                            if (pointer == 0)
                                throw new InvalidDataException("The native popup viewport returned no geometry.");
                            var bounds = *(Rect*)pointer;
                            (left, top, width, height) = (bounds.X, bounds.Y, bounds.Width, bounds.Height);
                        }
                        finally { if (pointer != 0) g_free(pointer); }
                    }
                    using (var states = new NativeObject(atspi_accessible_get_state_set(field)))
                        Check(atspi_state_set_contains(states.Value, 25) == 0,
                            "The source producer starts outside its owning native viewports, " + index);
                    Check(ScrollText(text.Value, target, target + 1, 6),
                        "Native range scrolling accepts an initially offscreen source and completes after ancestor publication, " + index);
                    foreach (var placement in new uint[] { 0, 1, 2, 3, 4, 5, 6 })
                    {
                        var accepted = ScrollText(text.Value, target, target + 1, placement);
                        var box = CharacterBox(text.Value, target, 1);
                        Check(accepted && box.X >= left - 1 && box.X + box.Width <= left + width + 1 && box.Y >= top - 1 && box.Y + box.Height <= top + height + 1
                            && TextAtPoint(text.Value, box.X + box.Width / 2, box.Y + box.Height / 2, 1) == target,
                            "Native nested source geometry is really visible at the original scalar after placement, " + index + "/" + placement);
                    }
                    foreach (var coordinates in new uint[] { 1, 0, 2 })
                    {
                        var local = CharacterBox(text.Value, target, 1);
                        var converted = CharacterBox(text.Value, target, coordinates);
                        Check(ScrollTextPoint(text.Value, target, target + 1, coordinates,
                            left + 60 + converted.X - local.X, top + 70 + converted.Y - local.Y),
                            "Native ancestor point request accepts the declared coordinate system, " + index + "/" + coordinates);
                        var positioned = CharacterBox(text.Value, target, 1);
                        Check(Math.Abs(positioned.X - left - 60) <= 1 && Math.Abs(positioned.Y - top - 70) <= 1,
                            "Native ancestor point completion publishes the actual requested glyph position, " + index + "/" + coordinates);
                    }
                    var before = CharacterBox(text.Value, target, 1);
                    Check(ScrollText(text.Value, target, target + 1, 6) && CharacterBox(text.Value, target, 1).X == before.X
                        && CharacterBox(text.Value, target, 1).Y == before.Y,
                        "Repeated native nearest reveal preserves an already visible nested source cell, " + index);
                    Check(!ScrollTextPoint(text.Value, target, target + 1, 1, left + width + 5, top + height + 5)
                        && CharacterBox(text.Value, target, 1).X == before.X && CharacterBox(text.Value, target, 1).Y == before.Y,
                        "A native point outside the enclosing viewport is refused without moving source geometry, " + index);
                    using (var states = new NativeObject(atspi_accessible_get_state_set(field)))
                        Check(ReadText(text.Value, 0, -1) == source && TextCaret(text.Value) == caret
                            && TextSelectionCount(text.Value) == selections && atspi_state_set_contains(states.Value, 12) == 0
                            && source.EnumerateRunes().Count() > target,
                            "Nested native reveal preserves original Unicode text, unrelated selection and keyboard focus, " + index);
                    var endOffset = source.EnumerateRunes().Count();
                    Check(ScrollText(text.Value, endOffset, endOffset, 6),
                        "Native empty EOF range completes through the nested ancestor planner, " + index);
                    var insertion = RangeBox(text.Value, endOffset, endOffset, 1);
                    Check(insertion.Width == 0 && insertion.X >= left - 1 && insertion.X <= left + width + 1
                        && insertion.Y >= top - 1 && insertion.Y + insertion.Height <= top + height + 1 && TextCaret(text.Value) == caret,
                        "Native EOF insertion geometry is actually visible while preserving the original source caret, " + index);
                }
                File.WriteAllText(output, JsonSerializer.Serialize(new LinuxAncestorResult(RuntimeInformation.FrameworkDescription, rightToLeft, virtualized, variableExtents, popupMode, checks.Count, checks, "Independent libatspi on actual owned X11/Vulkan source surfaces, including declared RTL/virtualized profile; not a physical reader test"), LinuxProbeResultsJson.Default.LinuxAncestorResult));
            }
            finally { foreach (var field in fields) field.Dispose(); }
        }
        finally { atspi_exit(); }
    }
}
