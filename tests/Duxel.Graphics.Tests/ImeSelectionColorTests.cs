using Duxel.Core;

internal static class ImeSelectionColorTests
{
    internal static void Run(UiForcedColorPalette palette, Action<bool, string> check)
    {
        foreach (var mode in new[] { "Input", "Segmented Input", "Textarea", "Wrapped Textarea", "Hard lines", "Markdown" })
            foreach (var reverse in new[] { false, true })
                RunCase(mode, reverse, palette, check);
    }

    private static void RunCase(string mode, bool reverse, UiForcedColorPalette palette, Action<bool, string> check)
    {
        using var candidate = new PresentationReuseTests.Fixture(1, true);
        using var reference = new PresentationReuseTests.Fixture(1, true);
        if (mode == "Segmented Input")
        {
            candidate.Context.SetPlatformTextBackend(new RunOnlyTextBackend());
            reference.Context.SetPlatformTextBackend(new RunOnlyTextBackend());
        }
        var initial = mode == "Markdown" ? "ABCD **EFGH** IJKL\n\nTail" : "ABCD EFGH IJKL\nTAIL";
        if (mode is "Input" or "Segmented Input")
            initial = initial.Split('\n')[0];
        const string preedit = "한";
        var insertion = reverse ? 4 : 10;
        var display = initial.Insert(insertion, preedit);
        var candidateText = new UiState<string>(initial);
        var referenceText = new UiState<string>(display);
        var ime = new MarkdownLifetimeTests.Ime();
        var referenceIme = new MarkdownLifetimeTests.Ime();
        // Compare selected text at a stable hidden-caret phase, rather than mixing caret color into glyph alpha.
        ime.AfterCaret = () => candidate.Context.State.AdvanceTime(.6f);
        referenceIme.AfterCaret = () => reference.Context.State.AdvanceTime(.6f);
        candidate.Context.SetImeHandler(ime);
        reference.Context.SetImeHandler(referenceIme);
        using var referenceTree = new UiAccessibilityTree();
        reference.Context.SetAccessibilityTree(referenceTree);
        var width = mode == "Wrapped Textarea" ? 96 : 220;
        UiWidget Make(UiState<string> value) => mode switch
        {
            "Input" or "Segmented Input" => Fluent.Input(string.Empty, value.Binding, 256, width),
            "Markdown" => Dux.MarkdownEditor("IME selection", value.Binding, new(Width: width, Height: 180)),
            _ => Fluent.Textarea(string.Empty, value.Binding, 256, width, 180)
        };
        using var actualWidget = Make(candidateText);
        using var expectedWidget = Make(referenceText);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiRect rawPreeditBounds = default;
        byte[] Frame(PresentationReuseTests.Fixture fixture, UiWidget widget, UiState<string> value, UiInputState? input = null)
        {
            using var drawing = fixture.Build(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                new UiControlContext(ui).RestartInputCaretBlink();
                ui.SetCursorScreenPos(new(10, 10));
                if (mode == "Hard lines")
                {
                    ui.SetNextItemWidth(width);
                    var text = value.Value;
                    if (ui.InputTextMultiline("##ime-selection", ref text, 256, 180f))
                        value.Value = text;
                    if (ReferenceEquals(fixture, reference))
                    {
                        var content = new UiMultilineInputLayout(new(10, 10, width, 180), ui.GetTextLineHeight()).ContentBounds;
                        rawPreeditBounds = new(content.X + ui.CalcTextSize(display[..insertion]).X, content.Y,
                            ui.CalcTextSize(preedit).X, ui.GetTextLineHeight());
                    }
                }
                else
                    widget.Render(ui);
            }, input ?? idle);
            return fixture.Render(drawing);
        }
        void Focus(PresentationReuseTests.Fixture fixture, UiWidget widget, UiState<string> text)
        {
            Frame(fixture, widget, text);
            Frame(fixture, widget, text, idle with { MousePosition = new(30, 26), LeftMousePressed = true, LeftMouseDown = true });
            Frame(fixture, widget, text, idle with { MousePosition = new(30, 26), LeftMouseReleased = true });
        }
        byte[] Key(UiKey key, KeyModifiers modifiers = default) => Frame(candidate, actualWidget, candidateText, idle with
        {
            KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)], Modifiers = modifiers
        });
        candidate.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
        reference.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
        Focus(candidate, actualWidget, candidateText);
        Focus(reference, expectedWidget, referenceText);
        var actualId = candidate.Context.State.ActiveId ?? throw new InvalidOperationException("Candidate IME editor did not accept real pointer focus.");
        var expectedId = reference.Context.State.ActiveId ?? throw new InvalidOperationException("Reference editor did not accept real pointer focus.");
        Key(UiKey.Home, KeyModifiers.Ctrl);
        void NavigateTo(int target, UiKey key, KeyModifiers modifiers = default)
        {
            for (var attempt = 0; attempt < initial.Length * 2; attempt++)
            {
                if (candidate.Context.State.GetSelection(actualId).End == target)
                    return;
                Key(key, modifiers);
            }
            throw new InvalidOperationException("Real arrow navigation did not reach the declared source endpoint.");
        }
        NavigateTo(reverse ? 10 : 4, UiKey.RightArrow);
        NavigateTo(reverse ? 4 : 10, reverse ? UiKey.LeftArrow : UiKey.RightArrow, KeyModifiers.Shift);
        var sourceSelection = reverse ? new UiTextSelection(10, 4) : new(4, 10);
        // The reference is committed display text with declared indices, not the production IME map.
        var displaySelection = reverse ? new UiTextSelection(11, 5) : new(4, 10);
        var referenceCaret = displaySelection.End;
        reference.Context.State.SetCursor(expectedId, referenceCaret);
        reference.Context.State.SetSelection(expectedId, new(referenceCaret, referenceCaret));
        var unselectedReference = Frame(reference, expectedWidget, referenceText);
        reference.Context.State.SetSelection(expectedId, displaySelection);
        var selectedReference = Frame(reference, expectedWidget, referenceText);
        var changedColor = new UiColor(190, 30, 80);
        reference.Context.SetTheme((palette with { CanvasText = changedColor }).Apply(UiCompiledDesign.Windows11Dark.Theme));
        var changedReference = Frame(reference, expectedWidget, referenceText);
        if (selectedReference.Length != candidate.FrameInfo.DisplaySize.X * candidate.FrameInfo.DisplaySize.Y * 4)
            throw new InvalidOperationException("IME comparison requires matching framebuffer extents.");
        var mask = new List<int>();
        for (var offset = 0; offset < selectedReference.Length; offset += 4)
            if (BitConverter.ToUInt32(unselectedReference, offset) != BitConverter.ToUInt32(selectedReference, offset))
                mask.Add(offset);
        var before = Frame(candidate, actualWidget, candidateText);
        var parsed = actualWidget is UiMarkdownEditor markdown ? markdown.Document.ParseCount : 0;
        ime.Composition = preedit;
        var actual = Frame(candidate, actualWidget, candidateText);
        var label = $"IME selection / {mode}, reverse={reverse}";
        check(candidateText.Value == initial && candidate.Context.State.GetSelection(actualId) == sourceSelection
            && (actualWidget is not UiMarkdownEditor editor || editor.Document.ParseCount == parsed),
            label + " / preedit preserves committed source, real Shift selection and original syntax parse");
        check(mask.Count > 20 && mask.All(offset => BitConverter.ToUInt32(actual, offset) == BitConverter.ToUInt32(selectedReference, offset)),
            label + " / actual selected pixels equal the independently selected committed display reference");
        candidate.Context.SetTheme((palette with { CanvasText = changedColor }).Apply(UiCompiledDesign.Windows11Dark.Theme));
        var changed = Frame(candidate, actualWidget, candidateText);
        var mismatch = mask.FirstOrDefault(offset => BitConverter.ToUInt32(changed, offset) != BitConverter.ToUInt32(selectedReference, offset), -1);
        var detail = mismatch < 0 ? string.Empty : $" / first=({mismatch / 4 % (int)candidate.FrameInfo.DisplaySize.X},{mismatch / 4 / (int)candidate.FrameInfo.DisplaySize.X}),"
            + $" before={BitConverter.ToUInt32(actual, mismatch):X8}, changed={BitConverter.ToUInt32(changed, mismatch):X8}, expected={BitConverter.ToUInt32(selectedReference, mismatch):X8}";
        check(mask.Count > 20 && mask.All(offset => BitConverter.ToUInt32(changed, offset) == BitConverter.ToUInt32(selectedReference, offset)),
            label + " / selected glyph antialiasing does not retain ordinary text color during preedit" + detail);
        check(mask.Count > 20 && mask.All(offset => BitConverter.ToUInt32(changedReference, offset) == BitConverter.ToUInt32(selectedReference, offset)),
            label + " / the independent non-IME reference also has isolated selected glyph alpha");
        UiRect bounds;
        if (mode == "Hard lines")
            bounds = rawPreeditBounds;
        else
        {
            if (!referenceTree.TryGet(expectedId, out var element) || element.Node.TextLayout is not { } textLayout)
                throw new InvalidOperationException("Reference IME text extents were not published.");
            bounds = textLayout.GetRangeExtents(insertion, insertion + preedit.Length);
        }
        var ordinaryChanges = 0;
        var ordinaryMatches = true;
        for (var y = Math.Max(0, (int)MathF.Ceiling(bounds.Y) + 2); y < Math.Min(candidate.FrameInfo.DisplaySize.Y, bounds.Y + bounds.Height - 2); y++)
            for (var x = Math.Max(0, (int)MathF.Ceiling(bounds.X) + 1); x < Math.Min(candidate.FrameInfo.DisplaySize.X, bounds.X + bounds.Width - 1); x++)
            {
                var offset = (y * (int)candidate.FrameInfo.DisplaySize.X + x) * 4;
                var oldPixel = BitConverter.ToUInt32(selectedReference, offset);
                var newPixel = BitConverter.ToUInt32(changedReference, offset);
                if (oldPixel == newPixel)
                    continue;
                ordinaryChanges++;
                ordinaryMatches &= BitConverter.ToUInt32(actual, offset) == oldPixel && BitConverter.ToUInt32(changed, offset) == newPixel;
            }
        check(ordinaryChanges > 0 && ordinaryMatches, label + " / the inserted Korean glyph keeps the independently rendered unselected foreground"
            + $" / bounds={bounds}, changed reference pixels={ordinaryChanges}");
        candidate.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
        ime.Composition = null;
        var cancelled = Frame(candidate, actualWidget, candidateText);
        check(before.AsSpan().SequenceEqual(cancelled) && candidateText.Value == initial,
            label + " / cancellation restores the exact original selected pixels and source");
    }
}
