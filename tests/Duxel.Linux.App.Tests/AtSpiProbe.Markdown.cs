using System.Runtime.InteropServices;
using System.Text.Json;

internal static unsafe partial class AtSpiProbe
{
    private static NativeObject OwnedApplication(uint parentPid)
    {
        using var desktop = new NativeObject(atspi_get_desktop(0));
        NativeObject? application = null;
        Wait(() =>
        {
            atspi_accessible_clear_cache(desktop.Value);
            for (var index = 0; index < Count(desktop.Value); index++)
            {
                using var candidate = Child(desktop.Value, index);
                nint error = 0;
                var pid = atspi_accessible_get_process_id(candidate.Value, &error);
                Error(ref error);
                if (pid != parentPid)
                    continue;
                application = new(g_object_ref(candidate.Value));
                return true;
            }
            return false;
        }, "the owned application's desktop registration");
        return application!;
    }

    internal static void RunMarkdown(uint parentPid, string output, nuint nativeWindow)
    {
        if (File.Exists(output))
            throw new IOException("The owned Markdown probe output already exists.");
        if (atspi_init() != 0)
            throw new InvalidOperationException("Independent Markdown AT-SPI initialization failed.");
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
            using var editor = Find(window.Value, value => Role(value) == 79 && Name(value) == "Markdown editor")
                ?? throw new InvalidOperationException("The native Markdown editor is missing.");
            using var viewer = Find(window.Value, value => Role(value) == 79 && Name(value) == "Markdown viewer")
                ?? throw new InvalidOperationException("The native Markdown viewer is missing.");
            using var text = new NativeObject(atspi_accessible_get_text_iface(editor.Value));
            using var edit = new NativeObject(atspi_accessible_get_editable_text_iface(editor.Value));
            using var view = new NativeObject(atspi_accessible_get_text_iface(viewer.Value));
            using var events = new EventWatch { Input = editor.Value };
            const string source = "# 정😀\n\n본문 **bold** and `code`";
            Check(ReadText(text.Value, 0, -1) == source, "Native Markdown Text returns the exact original source including hidden syntax");
            nint error = 0;
            var count = atspi_text_get_character_count(text.Value, &error);
            Error(ref error);
            Check(count == source.Length - 1 && ReadText(text.Value, 2, 4) == "정😀",
                "Native Markdown scalar counts and ranges preserve Korean and supplementary text");
            Check(CharacterBox(text.Value, 0, 1).Width == 0 && CharacterBox(text.Value, 1, 1).Width == 0,
                "Native hidden heading markers cannot claim rendered text width");
            var heading = CharacterBox(text.Value, 2, 1);
            var body = CharacterBox(text.Value, 6, 1);
            Check(heading.Width > 0 && heading.Height > body.Height && body.Y > heading.Y,
                "Native Markdown boxes retain actual heading and body typography positions");
            var emoji = CharacterBox(text.Value, 3, 1);
            VerifyBoundedText(text.Value, Check);
            Check(TextAtPoint(text.Value, emoji.X + emoji.Width / 2, emoji.Y + emoji.Height / 2, 1) == 3,
                "Native styled emoji hit testing returns its original scalar source offset");
            Check(TextUnit(text.Value, 2, 3) == (0, 5, "# 정😀\n"),
                "Native painted-line queries retain source delimiters and the real line separator");
            var defaults = atspi_text_get_default_attributes(text.Value, &error);
            try
            {
                Error(ref error);
                Check(defaults != 0 && g_hash_table_size(defaults) == 0,
                    "Styled native text omits invented uniform foreground and size defaults");
            }
            finally { if (defaults != 0) g_hash_table_unref(defaults); }
            Check(AttributeRun(text.Value, 0, false) == (0, 2, 0u, null)
                && AttributeValue(text.Value, 0, "family-name") == "" && AttributeValue(text.Value, 0, "size") == "",
                "Hidden Markdown syntax has an exact empty native attribute interval");
            Check(AttributeValue(text.Value, 2, "size") == "21" && AttributeValue(text.Value, 6, "size") == "10.5",
                "Native heading and body sizes follow their actual28/14 logical pixel typography scopes");
            Check(AttributeValue(text.Value, 2, "family-name") == "Malgun Gothic"
                && AttributeValue(text.Value, 3, "family-name") == "DejaVu Sans",
                "Styled native fonts follow actual Korean and emoji fallback selection in original source ranges");
            var headingAttributes = AttributeRun(text.Value, 2, false);
            Check(headingAttributes.Start == 2 && headingAttributes.End == 3 && headingAttributes.Count == 5
                && AttributeRun(text.Value, 2, true) == headingAttributes,
                "Styled explicit attributes include local paint and merge only identical exported source ranges");
            Check(AttributeValue(text.Value, 2, "weight") == "400",
                "A bold typography role using the fixture's regular font cannot fabricate a700 font weight");
            var before = events.TextSelections;
            var selected = atspi_text_add_selection(text.Value, 2, 4, &error);
            Error(ref error);
            var range = ReadSelection(text.Value);
            Check(selected != 0 && range.Start == 2 && range.End == 4 && TextCaret(text.Value) == 4,
                "Native Markdown selection targets the guarded original scalar range");
            Wait(() => { events.Check(); return events.TextSelections > before; }, "the Markdown selection event");
            Check(true, "Native Markdown source selection emits an independent selection event");
            var attributeEvents = events.TextAttributes;
            var sourceEvents = events.TextChanges.Count;
            using var component = new NativeObject(atspi_accessible_get_component_iface(editor.Value));
            var focused = atspi_component_grab_focus(component.Value, &error);
            Error(ref error);
            Wait(() => CharacterBox(text.Value, 0, 1).Width > 0, "the focused Markdown source block reveal");
            Check(focused != 0 && ReadText(text.Value, 0, -1) == source && TextCaret(text.Value) == 4,
                "Native focus reveals source syntax without changing source text or the selected caret");
            Wait(() => { events.Check(); return events.TextAttributes == attributeEvents + 1; }, "the actual revealed syntax attribute event");
            Check(AttributeValue(text.Value, 0, "fg-color") == "26985,26985,26985"
                && AttributeValue(text.Value, 0, "size") == "21" && events.TextChanges.Count == sourceEvents,
                "Revealed source syntax publishes its actual grey color and heading size with one attribute event and no source event");
            focused = atspi_component_grab_focus(component.Value, &error);
            Error(ref error);
            var quiet = System.Diagnostics.Stopwatch.StartNew();
            while (quiet.ElapsedMilliseconds < 200)
            {
                Pump();
                events.Check();
                Thread.Sleep(10);
            }
            Check(focused != 0 && events.TextAttributes == attributeEvents + 1,
                "Repainting the same focused styled projection does not duplicate native attribute notifications");
            using (var peer = new NativePeer(nativeWindow))
                Check(peer.HasNativeFocus(), "The native Markdown focus action reaches actual X11 keyboard focus");
            var changes = events.TextChanges.Count;
            Check(InsertText(edit.Value, 2, "한", 3) && ReadText(text.Value, 0, -1) == "# 한정😀\n\n본문 **bold** and `code`",
                "Native Markdown insertion edits source and reparses the actual styled editor");
            var deleted = atspi_editable_text_delete_text(edit.Value, 2, 3, &error);
            Error(ref error);
            Check(deleted != 0 && ReadText(text.Value, 0, -1) == source,
                "Native Markdown deletion restores original source using scalar range conversion");
            Wait(() => { events.Check(); return events.TextChanges.Count >= changes + 2; }, "Markdown insertion/deletion events");
            Check(events.TextChanges.Skip(changes).Any(change => change.Start == 2 && change.Length == 1 && change.Text == "한"),
                "Native Markdown mutation events expose actual source scalar ranges and text");
            const string code = "```csharp\nvar value = 1;\n```";
            Check(SetText(edit.Value, code) && ReadText(text.Value, 0, -1) == code,
                "Native Markdown replacement preserves complete fenced source rather than projected code");
            var codeBox = CharacterBox(text.Value, 10, 1);
            Check(codeBox.Width > 0 && TextAtPoint(text.Value, codeBox.X + codeBox.Width / 2, codeBox.Y + codeBox.Height / 2, 1) == 10,
                "Native fenced-code geometry still maps to its original source position");
            void Shortcut(nuint key, string expected)
            {
                using var peer = new NativePeer(nativeWindow);
                peer.Key(true, 0xffe3);
                peer.Key(true, key, 4);
                try
                {
                    Wait(() => ReadText(text.Value, 0, -1) == expected, "the owned Markdown keyboard history operation");
                }
                finally
                {
                    peer.Key(false, key, 4);
                    peer.Key(false, 0xffe3, 4);
                }
            }
            Shortcut(0x7a, source);
            Check(ReadText(text.Value, 0, -1) == source,
                "Real X11 Ctrl+Z undoes native Markdown replacement through the shared source history");
            Shortcut(0x79, code);
            Check(ReadText(text.Value, 0, -1) == code,
                "Real X11 Ctrl+Y redoes source replacement and reparses styled code geometry");
            Check(AttributeValue(text.Value, 10, "family-name") == "DejaVu Sans" && AttributeValue(text.Value, 10, "size") == "10.5",
                "Fenced code publishes the fixture's actual configured monospace font resource and point size");
            const string styles = "**bold** and *italic* [link](https://example.com)";
            Check(SetText(edit.Value, styles), "Native source replacement switches to mixed bold, oblique and link paint");
            Check(AttributeValue(text.Value, styles.IndexOf("italic", StringComparison.Ordinal), "style") == "oblique",
                "Styled native emphasis reports the actual simulated oblique variant");
            Check(AttributeValue(text.Value, styles.IndexOf("link", StringComparison.Ordinal), "fg-color") == "0,26471,49344",
                "Styled native link color matches the actual light-scheme link painter");
            using var unwrapped = Find(window.Value, value => Role(value) == 79 && Name(value) == "Unwrapped editor")
                ?? throw new InvalidOperationException("The native unwrapped editor is missing.");
            using var unwrappedText = new NativeObject(atspi_accessible_get_text_iface(unwrapped.Value));
            using var unwrappedEdit = new NativeObject(atspi_accessible_get_editable_text_iface(unwrapped.Value));
            const string unwrappedSource = "정😀\n\nend\n";
            Check(ReadText(unwrappedText.Value, 0, -1) == unwrappedSource && ReadText(unwrappedText.Value, 0, 2) == "정😀",
                "Native unwrapped multiline exposes exact committed Korean and supplementary source ranges");
            var unwrappedEmoji = CharacterBox(unwrappedText.Value, 1, 1);
            Check(unwrappedEmoji.Width > 0 && TextAtPoint(unwrappedText.Value, unwrappedEmoji.X + unwrappedEmoji.Width / 2,
                unwrappedEmoji.Y + unwrappedEmoji.Height / 2, 1) == 1,
                "Native unwrapped source geometry maps a painted emoji back to its original scalar");
            Check(TextUnit(unwrappedText.Value, 4, 3) == (4, 8, "end\n"),
                "Native unwrapped line queries preserve blank and trailing source separators");
            Check(AttributeValue(unwrappedText.Value, 0, "size") == "9",
                "Native unwrapped geometry and paint use the actual small textarea's12 logical pixel typography");
            var unwrappedRange = RangeBox(unwrappedText.Value, 0, 2, 1);
            Check(unwrappedRange.Width > unwrappedEmoji.Width && unwrappedRange.Height == unwrappedEmoji.Height,
                "Native unwrapped range bounds span both painted source characters");
            var longSource = new string('W', 70) + "\nend\n";
            Check(SetText(unwrappedEdit.Value, longSource) && ReadText(unwrappedText.Value, 0, -1) == longSource,
                "Native replacement retains unwrapped long source without introducing soft line breaks");
            var offscreen = CharacterBox(unwrappedText.Value, 60, 1);
            Check(TextAtPoint(unwrappedText.Value, offscreen.X + offscreen.Width / 2, offscreen.Y + offscreen.Height / 2, 1) == -1,
                "Native unwrapped clipped text cannot claim an offscreen hit");
            VerifyTextScroll(unwrapped.Value, unwrappedText.Value, unwrappedEdit.Value, Check);
            Check(SetText(unwrappedEdit.Value, unwrappedSource), "The owned native unwrapped probe restores its original source");
            Check(ScrollText(unwrappedText.Value, 0, 1, 6) && ReadText(unwrappedText.Value, 0, -1) == unwrappedSource,
                "Native visible-range scrolling succeeds without needing a scrollbar or changing the source");
            VerifySingleLineScroll(window.Value, nativeWindow, Check);
            var table = ReadText(view.Value, 0, -1);
            var a = CharacterBox(view.Value, table.LastIndexOf('A'), 1);
            var b = CharacterBox(view.Value, table.LastIndexOf('B'), 1);
            Check(a.Width > 0 && b.X > a.X + a.Width && b.Y == a.Y,
                "Native rendered table geometry preserves column alignment and padding");
            Check(CharacterBox(view.Value, 0, 1).Width == 0 && CharacterBox(view.Value, table.LastIndexOf('|'), 1).Width == 0,
                "Native hidden table delimiters cannot claim column gaps");
            Check(TextAtPoint(view.Value, b.X + b.Width / 2, b.Y + b.Height / 2, 1) == table.LastIndexOf('B'),
                "Native table cell hit testing returns the correct original source position");
            using var readonlyEdit = new NativeObject(atspi_accessible_get_editable_text_iface(viewer.Value));
            Check(!SetText(readonlyEdit.Value, "forbidden") && ReadText(view.Value, 0, -1) == table,
                "Native read-only Markdown rejects mutation and preserves its source");
            Check(SetText(edit.Value, source) && ReadText(text.Value, 0, -1) == source,
                "The owned native Markdown probe restores the editor's original source");
            File.WriteAllText(output, JsonSerializer.Serialize(new LinuxMarkdownResult(RuntimeInformation.FrameworkDescription, checks.Count, checks, false, false, "Actual product renderer and independent AT-SPI source/geometry/editing; no physical screen reader qualification"), LinuxProbeResultsJson.Default.LinuxMarkdownResult));
        }
        finally { atspi_exit(); }
    }
}
