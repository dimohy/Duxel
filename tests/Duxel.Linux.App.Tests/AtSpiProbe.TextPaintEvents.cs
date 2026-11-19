internal static unsafe partial class AtSpiProbe
{
    private static void VerifyPaintEvents(nint input, EventWatch events, string output, Action<bool, string> check)
    {
        using var text = new NativeObject(atspi_accessible_get_text_iface(input));
        var before = events.TextAttributes;
        var changes = events.TextChanges.Count;
        var caret = TextCaret(text.Value);
        var source = ReadText(text.Value, 0, -1);
        var request = output + ".paint-state";
        void Request(string state)
        {
            // Immutable markers avoid cross-filesystem overwrite/unlink races on WSL mounts.
            using var marker = new FileStream(request + "." + state, FileMode.CreateNew);
        }
        Request("1");
        Wait(() => { events.Check(); return events.TextAttributes == before + 1 && AttributeValue(text.Value, 0, "fg-color") == "4369,21331,51657"; },
            "the actual editor color change and native text-attributes-changed event");
        check(events.TextAttributes == before + 1 && ReadText(text.Value, 0, -1) == source && TextCaret(text.Value) == caret
            && events.TextChanges.Count == changes, "Actual foreground change emits one native attribute event while preserving source and caret");
        Request("2");
        Wait(() => { events.Check(); return events.TextAttributes == before + 2 && AttributeValue(text.Value, 0, "fg-color") == "7967,7967,7967"; },
            "the actual editor foreground restoration and its native event");
        check(events.TextAttributes == before + 2, "Restoring actual source paint emits exactly one further attribute notification");
        Request("3");
        Wait(() => File.Exists(request + ".ack.3"), "an explicitly repainted unchanged source color");
        var quiet = System.Diagnostics.Stopwatch.StartNew();
        while (quiet.ElapsedMilliseconds < 200)
        {
            Pump();
            events.Check();
            Thread.Sleep(10);
        }
        check(AttributeValue(text.Value, 0, "fg-color") == "7967,7967,7967" && events.TextAttributes == before + 2,
            "Repainting unchanged source attributes does not emit a duplicate native notification");
        before = events.TextAttributes;
        Request("4");
        Wait(() => { events.Check(); return events.TextAttributes == before + 1 && AttributeValue(text.Value, 0, "family-name") == "맑은 고딕"; },
            "the explicitly selected Korean font name and native attribute notification");
        check(ReadText(text.Value, 0, -1) == source && TextCaret(text.Value) == caret,
            "Changing explicit font-name language emits an attribute event while preserving actual editor source and caret");
        Request("5");
        Wait(() => { events.Check(); return events.TextAttributes == before + 2 && AttributeValue(text.Value, 0, "family-name") == "Malgun Gothic"; },
            "the restored English font name and native attribute notification");
        check(events.TextAttributes == before + 2, "Restoring the actual exported font name emits exactly one further notification");
        Request("6");
        Wait(() => File.Exists(request + ".ack.6"), "an unchanged font metadata repaint");
        quiet.Restart();
        while (quiet.ElapsedMilliseconds < 200)
        {
            Pump();
            events.Check();
            Thread.Sleep(10);
        }
        check(AttributeValue(text.Value, 0, "family-name") == "Malgun Gothic" && events.TextAttributes == before + 2,
            "Repainting unchanged actual font metadata does not emit a duplicate native notification");
        before = events.TextAttributes;
        Request("7");
        Wait(() => { events.Check(); return events.TextAttributes == before + 1 && AttributeValue(text.Value, 0, "size") == "18"; },
            "the actual24 logical pixel font and its18-point native size notification");
        check(ReadText(text.Value, 0, -1) == source && TextCaret(text.Value) == caret,
            "Actual font-size change emits a native attribute event without changing source or caret");
        Request("8");
        Wait(() => { events.Check(); return events.TextAttributes == before + 2 && AttributeValue(text.Value, 0, "size") == "13.5"; },
            "the restored actual13.5-point editor font");
        check(events.TextAttributes == before + 2, "Restoring the nominal font size emits exactly one further native event");
        Request("9");
        Wait(() => File.Exists(request + ".ack.9"), "an unchanged nominal-size repaint");
        quiet.Restart();
        while (quiet.ElapsedMilliseconds < 200)
        {
            Pump();
            events.Check();
            Thread.Sleep(10);
        }
        check(AttributeValue(text.Value, 0, "size") == "13.5" && events.TextAttributes == before + 2,
            "Repainting the same nominal font size does not emit a duplicate native notification");
    }
}
