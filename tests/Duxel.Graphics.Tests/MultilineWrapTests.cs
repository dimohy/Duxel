using Duxel.Core;

internal static class MultilineWrapTests
{
    internal static void Run(Action<bool, string> check)
    {
        var glyphs = Enumerable.Range(32, 95).ToDictionary(code => code,
            code => new UiGlyphInfo(7, 0, -10, code == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
        glyphs[0x1f600] = new(14, 0, -10, 13, 12, new(0, 0, 1, 1));
        glyphs[0x301] = new(0, 0, -10, 0, 12, new(0, 0, 1, 1));
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
            glyphs, new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        const string original = "aaaa bbbb cccc";
        var text = original;
        var reads = 0;
        var writes = 0;
        var settings = UiTextSettings.Default;
        using var editor = new UiMultilineInput<UiStandardMultilineInputBehavior, Presentation>("Wrapped",
            new(() =>
            {
                reads++;
                return text;
            }, value =>
            {
                writes++;
                text = value;
            }), 4096, 49, 64);
        using var parent = Dux.SizedBox(editor, 49, 64);
        void Frame(UiInputState input, Action<UiImmediateContext>? probe = null, UiContext? target = null)
        {
            var activeContext = target ?? context;
            activeContext.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), settings);
            activeContext.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 20));
                if (probe is not null)
                    probe(ui);
                else
                    parent.Render(ui);
            }));
            using var data = activeContext.GetDrawData();
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, modifiers)],
                Modifiers = modifiers
            });
        }
        Presentation.Last = default;
        Frame(idle, ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            check(parent.MeasureConstrained(ui, new()) == new UiVector2(49, 64)
                && reads == 0 && writes == 0 && cursor == ui.GetCursorScreenPos() && Presentation.Last.VisualLines is null,
                "wrapped measurement does not read text or realize visual lines");
        });
        Frame(idle);
        var visual = Presentation.Last.VisualLines!;
        check(visual.Count == 3 && visual[0].Text == "aaaa " && visual[1].Text == "bbbb " && visual[2].Text == "cccc",
            "soft wrapping retains word spaces and maps three visual rows");
        check(text == original && writes == 0 && Presentation.Last.MaximumScrollX == 0,
            "soft wrapping never inserts model newlines or exposes a horizontal scrollbar");
        var builds = visual.Builds;
        Frame(idle);
        check(ReferenceEquals(visual, Presentation.Last.VisualLines) && visual.Builds == builds,
            "unchanged wrapped geometry reuses its owned text and font keyed line data");
        Frame(idle with
        {
            MousePosition = new(27, 24),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            LeftMouseReleased = true
        });
        check(Presentation.Last.CaretIndex == 1, "wrapped hit testing returns an original UTF16 index");
        Key(UiKey.End);
        check(Presentation.Last.CaretIndex == 5 && visual.FindLine(5) == 0,
            "wrapped End preserves trailing visual-line affinity at a shared logical boundary");
        Key(UiKey.Home);
        check(Presentation.Last.CaretIndex == 0, "wrapped Home targets the current visual row start");
        Key(UiKey.DownArrow);
        check(Presentation.Last.CaretIndex == 5 && visual.FindLine(5) == 1,
            "wrapped Down targets the next visual row instead of the next hard newline");
        Key(UiKey.End);
        Key(UiKey.DownArrow, KeyModifiers.Shift);
        check(Presentation.Last.CaretIndex == 14 && Presentation.Last.Selection == new UiTextSelection(10, 14),
            "wrapped Shift Down selects an original-text range across visual rows");
        Key(UiKey.UpArrow);
        check(Presentation.Last.CaretIndex == 10 && visual.FindLine(10) == 1,
            "wrapped vertical navigation preserves preferred X across a shorter row");
        Key(UiKey.Home, KeyModifiers.Ctrl);
        Key(UiKey.PageDown);
        check(Presentation.Last.CaretIndex == 10, "wrapped PageDown uses visible line-box height and clamps to the last row");
        Key(UiKey.Home, KeyModifiers.Ctrl);
        Frame(idle with
        {
            KeyEvents = [new(UiKey.DownArrow, true, default)]
        });
        context.State.SetRepeatTime(UiKey.DownArrow, context.State.TimeSeconds - 1);
        Frame(idle);
        Frame(idle with
        {
            KeyEvents = [new(UiKey.DownArrow, false, default)]
        });
        check(Presentation.Last.CaretIndex == 10, "wrapped held-key repeat follows visual rows");
        Key(UiKey.End, KeyModifiers.Ctrl);
        Frame(idle with
        {
            CharEvents = [new('x')]
        });
        check(text == original + "x" && writes == 1, "wrapped text insertion changes the model exactly once");
        Key(UiKey.Z, KeyModifiers.Ctrl);
        check(text == original && writes == 2, "wrapped undo restores model text without generated soft newlines");
        Key(UiKey.Y, KeyModifiers.Ctrl);
        check(text == original + "x" && writes == 3, "wrapped redo restores the actual edit");
        Key(UiKey.Z, KeyModifiers.Ctrl);
        parent.Options = new(35, 32);
        Frame(idle);
        var expectedWidth = MathF.Max(0, 35 - context.GetStyleData().ScrollbarSize);
        check(Presentation.Last.VisualContent!.Value.Width == expectedWidth && Presentation.Last.MaximumScrollY > 0
            && Presentation.Last.MaximumScrollX == 0 && text == original,
            "wrapped vertical overflow reserves its real scrollbar gutter and reflows without changing text");
        var bottom = Presentation.Last.ContentHeight;
        check(Presentation.Last.ScrollY + 32 >= bottom,
            "wrapped caret visibility follows the visual tail after parent reallocation");
        parent.Options = new(49, 64);
        text = "ab\n\ncd\n";
        Frame(idle);
        check(visual.Count == 4 && visual[1].Text.Length == 0 && visual[3].Text.Length == 0 && text == "ab\n\ncd\n",
            "wrapped layout preserves empty hard lines and a trailing newline");
        text = "😀e\u0301";
        Frame(idle);
        context.State.ActiveId = Presentation.Last.Id;
        context.State.SetCursor(Presentation.Last.Id, text.Length);
        Key(UiKey.Backspace);
        check(text == "😀", "wrapped Backspace removes one complete combining grapheme");
        Key(UiKey.LeftArrow);
        check(Presentation.Last.CaretIndex == 0, "wrapped Left does not split a surrogate pair");
        Key(UiKey.Delete);
        check(text.Length == 0, "wrapped Delete removes a complete surrogate grapheme");
        text = "e\u0301";
        Frame(idle);
        context.State.SetSelection(Presentation.Last.Id, new(1, 2));
        context.State.SetCursor(Presentation.Last.Id, 2);
        Key(UiKey.Backspace);
        check(text.Length == 0, "wrapped selection replacement aligns to whole combining graphemes");
        text = original;
        parent.Options = new(35, 64);
        Frame(idle);
        builds = visual.Builds;
        settings = settings with
        {
            Scale = 1.25f
        };
        Frame(idle);
        check(visual.Builds > builds, "wrapped layout invalidates advances when font scale changes");
        builds = visual.Builds;
        settings = settings with
        {
            MissingGlyphObserver = static _ => { }
        };
        Frame(idle);
        Frame(idle);
        check(visual.Builds >= builds + 2,
            "wrapped layout does not retain cross-frame measurement when glyph observation disables retention");
        var ime = new CompositionIme { Composition = "zzzz zzzz zzzz" };
        text = "ab";
        context.SetImeHandler(ime);
        context.State.ActiveId = Presentation.Last.Id;
        context.State.SetCursor(Presentation.Last.Id, 1);
        var beforeComposition = writes;
        Frame(idle);
        var composing = Presentation.Last;
        var composingLayout = Presentation.LastLayout;
        check(text == "ab" && writes == beforeComposition && composing.CompositionLines!.Count > visual.Count
            && composing.ScrollY > 0 && composing.MaximumScrollX == 0,
            "wrapped IME preedit uses visual rows and caret scrolling without modifying the model");
        ime.Composition = null;
        Frame(idle);
        check(text == "ab" && Presentation.Last.CompositionLines is null && Presentation.Last.ScrollY == 0,
            "ending wrapped IME preedit releases preview rows and restores the model viewport");
        Frame(idle, ui =>
        {
            var rejected = false;
            try
            {
                new UiControlContext(ui).PaintEditableMultiline(composingLayout, composing,
                    new(new(255, 255, 255), new(60, 100, 160), new(255, 255, 255)));
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            check(rejected, "wrapped paint rejects a stale snapshot after its owned line data changes");
        });
        using var other = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255],
            glyphs, new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
        var version = visual.Version;
        Frame(idle, target: other);
        check(visual.Version > version && context.State.TextInputStorage == (0, 0) && context.State.ScrollStorage == (0, 0),
            "wrapped context transfer rebuilds line resources and releases previous input storage");
        parent.Dispose();
        check(visual.Count == 0 && other.State.TextInputStorage == (0, 0) && other.State.ScrollStorage == (0, 0),
            "wrapped disposal releases line strings, editor history and scrollbar storage");
    }

    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        internal static UiMultilineInputLayout LastLayout;
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            Layout(width, height);
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
            Layout(size.X, size.Y);
        private static UiMultilineInputLayout Layout(float width, float height) =>
            new(new(0, 0, width, height), 16)
            {
                Content = new(0, 0, width, height),
                Wrap = true
            };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
        {
            Last = edit;
            LastLayout = layout;
            context.PaintEditableMultiline(layout, edit, new(new(255, 255, 255), new(60, 100, 160), new(255, 255, 255)));
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private sealed class CompositionIme : IUiImeHandler
    {
        internal string? Composition;
        public string? ConsumeCommittedText(string inputId) => null;
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public string? GetCompositionText() => Composition;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
        }
        public void SetCompositionOwner(string? inputId)
        {
        }
    }
}
