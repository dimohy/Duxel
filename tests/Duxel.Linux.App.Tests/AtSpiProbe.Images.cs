using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Duxel.Linux.Sample;

internal static unsafe partial class AtSpiProbe
{
    internal static void RunImages(uint parentPid, string output, nuint nativeWindow)
    {
        if (File.Exists(output))
            throw new IOException("Use a fresh native image result path.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent image AT-SPI initialization failed.");
        atspi_set_timeout(2000, 2000);
        var checks = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            checks.Add(message);
            Console.WriteLine("PASS " + message);
        }
        Rect Box(nint accessible)
        {
            using var component = new NativeObject(atspi_accessible_get_component_iface(accessible));
            nint error = 0;
            var pointer = atspi_component_get_extents(component.Value, 1, &error);
            try
            {
                Error(ref error);
                if (pointer == 0)
                    throw new InvalidDataException("The image component returned no geometry.");
                return *(Rect*)pointer;
            }
            finally { if (pointer != 0) g_free(pointer); }
        }
        try
        {
            using var app = OwnedApplication(parentPid);
            using var window = Child(app.Value, 0);
            using var document = Find(window.Value, value => Role(value) == 79 && Name(value) == "Images")
                ?? throw new InvalidOperationException("The image document was not exported.");
            using var text = new NativeObject(atspi_accessible_get_text_iface(document.Value));
            var source = ReadText(text.Value, 0, -1);
            var caret = TextCaret(text.Value);
            var selectionCount = TextSelectionCount(text.Value);
            var pictures = new List<NativeObject>();
            void Collect(nint parent)
            {
                for (var index = 0; index < Count(parent); index++)
                {
                    using var child = Child(parent, index);
                    if (Role(child.Value) == 27)
                        pictures.Add(new(g_object_ref(child.Value)));
                    Collect(child.Value);
                }
            }
            Collect(document.Value);
            try
            {
                Check(pictures.Count == 7 && pictures.Select(p => Name(p.Value)).ToHashSet().SetEquals(
                    new[] { "한글 그림", "second", "Image unavailable: missing", "linked", "leading", "table", "tail" }),
                    "Independent native traversal exposes descriptive images and omits decorative images");
                var first = pictures.Single(p => Name(p.Value) == "한글 그림").Value;
                var firstBox = Box(first);
                VerifyImageInterface(first, firstBox, Check);
                var unavailable = pictures.Single(p => Name(p.Value) == "Image unavailable: missing").Value;
                Check(atspi_accessible_get_image_iface(unavailable) == 0,
                    "An unavailable caption does not advertise the native Image interface");
                Check(firstBox.Width is >= 120 and <= 121 && firstBox.Height is >= 60 and <= 61,
                    $"Native image component bounds enclose the actual 120x60 rectangle at fractional pixel coordinates, actual={firstBox.Width}x{firstBox.Height}");
                using (var windowComponent = new NativeObject(atspi_accessible_get_component_iface(window.Value)))
                {
                    nint error = 0;
                    using var hit = new NativeObject(atspi_component_get_accessible_at_point(windowComponent.Value,
                        firstBox.X + 60, firstBox.Y + 30, 1, &error));
                    Error(ref error);
                    Check(Name(hit.Value) == "한글 그림" && Role(hit.Value) == 27,
                        "A native hit at the rendered image resolves its real image child");
                }
                nint parentError = 0;
                using (var parent = new NativeObject(atspi_accessible_get_parent(first, &parentError)))
                {
                    Error(ref parentError);
                    Check(Name(parent.Value) == "Images", "An ordinary image belongs to its original document");
                }
                var linked = pictures.Single(p => Name(p.Value) == "linked").Value;
                using (var parent = new NativeObject(atspi_accessible_get_parent(linked, &parentError)))
                {
                    Error(ref parentError);
                    Check(Role(parent.Value) == 88, "An image label belongs to the actual native link");
                }
                var tail = pictures.Single(p => Name(p.Value) == "tail").Value;
                using (var tailImage = new NativeObject(atspi_accessible_get_image_iface(tail)))
                {
                    nint sizeError = 0;
                    var sizePointer = atspi_image_get_image_size(tailImage.Value, &sizeError);
                    try
                    {
                        Error(ref sizeError);
                        var size = Marshal.PtrToStructure<Point>(sizePointer);
                        Check(size.X is >= 120 and <= 121 && size.Y is >= 60 and <= 61,
                            "The native Image size remains complete while its Component is clipped offscreen");
                    }
                    finally { if (sizePointer != 0) g_free(sizePointer); }
                }
                using (var states = new NativeObject(atspi_accessible_get_state_set(tail)))
                    Check(atspi_state_set_contains(states.Value, 25) == 0,
                        "A clipped image remains discoverable without falsely reporting that it is showing");
                foreach (var name in new[] { "leading", "table", "tail" })
                {
                    var marker = "![" + name + "](asset.png)";
                    var utf16 = source.IndexOf(marker, StringComparison.Ordinal);
                    if (utf16 < 0)
                        throw new InvalidDataException("Missing original image source marker.");
                    var start = source[..utf16].EnumerateRunes().Count();
                    Check(ScrollText(text.Value, start, start + marker.EnumerateRunes().Count(), 6),
                        "Native document source-range reveal reaches its image, " + name);
                    var box = Box(pictures.Single(p => Name(p.Value) == name).Value);
                    Check(box.Width is >= 120 and <= 121 && box.Height is >= 60 and <= 61 && box.Y >= 0 && box.Y + box.Height <= 260,
                        $"Revealed image publishes its enclosing pixel geometry, {name}; actual={box.Width}x{box.Height}");
                }
                Check(ReadText(text.Value, 0, -1) == source && TextCaret(text.Value) == caret && TextSelectionCount(text.Value) == selectionCount,
                    "Native image discovery and reveal preserve original Markdown source, caret and selection");
                using var peer = new NativePeer(nativeWindow);
                using var documentComponent = new NativeObject(atspi_accessible_get_component_iface(document.Value));
                nint focusError = 0;
                Check(atspi_component_grab_focus(documentComponent.Value, &focusError) != 0,
                    "The real Markdown viewer accepts focus before native image keyboard selection");
                Error(ref focusError);
                var imageSourceStart = source.IndexOf("![", StringComparison.Ordinal);
                var imageSourceEnd = source.IndexOf(')', imageSourceStart) + 1;
                var firstStart = source[..imageSourceStart].EnumerateRunes().Count();
                var firstEnd = source[..imageSourceEnd].EnumerateRunes().Count();
                Check(atspi_text_set_caret_offset(text.Value, firstStart, &focusError) != 0,
                    "Native image keyboard selection begins at the actual committed source boundary");
                Error(ref focusError);
                Check(ScrollText(text.Value, firstStart, firstEnd, 6), "The image keyboard fixture reveals its actual picture before reading pixels");
                var actualBox = Box(first);
                (byte R, byte G, byte B) Sample(string suffix)
                {
                    (byte R, byte G, byte B) pixel = default;
                    peer.Capture(output + suffix + ".ppm", LinuxDemoScreen.WindowWidth, LinuxDemoScreen.WindowHeight, pixels =>
                    {
                        var at = ((actualBox.Y + actualBox.Height / 2) * LinuxDemoScreen.WindowWidth + actualBox.X + actualBox.Width / 2) * 3;
                        pixel = (pixels[at], pixels[at + 1], pixels[at + 2]);
                    });
                    return pixel;
                }
                Wait(() => Sample(".before") == ((byte)53, (byte)168, (byte)124), "the actual opaque Markdown image pixel");
                var baseline = Sample(".before");
                peer.Key(true, 0xff53, 1);
                peer.Key(false, 0xff53, 1);
                Wait(() => TextCaret(text.Value) == firstEnd && TextSelectionCount(text.Value) == 1,
                    "one real X11 Shift+Right selecting the whole image");
                Check(TextCaret(text.Value) == firstEnd, "A single native Shift+Right crosses the complete hidden image URI");
                Wait(() => Sample(".selected-right") != baseline, "the selection tint above the opaque image");
                Check(Sample(".selected-right") != baseline, "Actual product-window pixels show selection above the opaque image");
                Check(atspi_text_set_caret_offset(text.Value, firstEnd, &focusError) != 0, "The real image selection can be collapsed at its trailing boundary");
                Error(ref focusError);
                Wait(() => Sample(".cleared") == baseline, "the restored unselected image pixel");
                Check(TextSelectionCount(text.Value) == 0, "Collapsing image selection removes its visible tint and source range");
                peer.Key(true, 0xff51, 1);
                peer.Key(false, 0xff51, 1);
                Wait(() => TextCaret(text.Value) == firstStart && TextSelectionCount(text.Value) == 1,
                    "one real X11 Shift+Left selecting the whole image");
                Wait(() => Sample(".selected-left") != baseline, "the reverse image-selection tint");
                Check(ReadText(text.Value, 0, -1) == source, "Reverse native image selection preserves the original Markdown source");
            }
            finally { foreach (var picture in pictures) picture.Dispose(); }
        }
        finally { atspi_exit(); }
        File.WriteAllText(output, JsonSerializer.Serialize(new LinuxQualifiedResult(RuntimeInformation.FrameworkDescription, checks.Count, checks, "Native Image description, locale, size, coordinate spaces and clipped Component geometry on an owned X11/Vulkan window; not a physical reader"), LinuxProbeResultsJson.Default.LinuxQualifiedResult));
    }

    private static void VerifyImageInterface(nint accessible, Rect component, Action<bool, string> check)
    {
        using var image = new NativeObject(atspi_accessible_get_image_iface(accessible));
        string Property(bool locale)
        {
            nint error = 0;
            var pointer = locale ? atspi_image_get_image_locale(image.Value, &error)
                : atspi_image_get_image_description(image.Value, &error);
            try
            {
                Error(ref error);
                return Marshal.PtrToStringUTF8(pointer) ?? throw new InvalidDataException("No native image string.");
            }
            finally { if (pointer != 0) g_free(pointer); }
        }
        check(Property(false) == "한글 그림" && Property(true) == "",
            "Native Image properties preserve Korean alternative text and an unspecified locale");
        for (uint coordinates = 0; coordinates <= 2; coordinates++)
        {
            nint error = 0;
            var extentsPointer = atspi_image_get_image_extents(image.Value, coordinates, &error);
            var positionPointer = atspi_image_get_image_position(image.Value, coordinates, &error);
            var sizePointer = atspi_image_get_image_size(image.Value, &error);
            try
            {
                Error(ref error);
                var extents = Marshal.PtrToStructure<Rect>(extentsPointer);
                var position = Marshal.PtrToStructure<Point>(positionPointer);
                var size = Marshal.PtrToStructure<Point>(sizePointer);
                check(extents.X == position.X && extents.Y == position.Y && extents.Width == size.X && extents.Height == size.Y,
                    "Native Image size, position and extents agree in coordinate space " + coordinates);
                if (coordinates == 1)
                    check(extents.X == component.X && extents.Y == component.Y && extents.Width == component.Width && extents.Height == component.Height,
                        "A fully visible native image has the same window rectangle as its actual Component");
            }
            finally
            {
                if (extentsPointer != 0)
                    g_free(extentsPointer);
                if (positionPointer != 0)
                    g_free(positionPointer);
                if (sizePointer != 0)
                    g_free(sizePointer);
            }
        }
    }

    [LibraryImport(Library)] private static partial nint atspi_accessible_get_image_iface(nint value);
    [LibraryImport(Library)] private static partial nint atspi_image_get_image_description(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_image_get_image_locale(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_image_get_image_size(nint value, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_image_get_image_position(nint value, uint coordinates, nint* error);
    [LibraryImport(Library)] private static partial nint atspi_image_get_image_extents(nint value, uint coordinates, nint* error);
}
