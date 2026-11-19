using Duxel.Core;

internal static class MultilineElementTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var mode in new[] { "plain", "wrapped", "hard" })
        {
            using var editor = new Editor(mode);
            void Check(bool condition, string message) => check(condition, mode + " multiline elements : " + message);
            editor.Reset("A😀e\u0301Z\nx", 5);
            editor.Key(UiKey.LeftArrow);
            Check(editor.Caret == 3, "Left crosses a complete combining element");
            editor.Key(UiKey.LeftArrow);
            Check(editor.Caret == 1, "Left crosses a complete supplementary element");
            editor.Key(UiKey.RightArrow);
            editor.Key(UiKey.RightArrow, KeyModifiers.Shift);
            Check(editor.Selection == new UiTextSelection(3, 5), "Shift Right selects a complete combining element");
            editor.Key(UiKey.C, KeyModifiers.Ctrl);
            Check(editor.Clipboard.Text == "e\u0301", "copy preserves the complete selected element");
            editor.Key(UiKey.Delete);
            Check(editor.Text == "A😀Z\nx", "selected deletion leaves surrounding source intact");
            editor.Key(UiKey.Z, KeyModifiers.Ctrl);
            Check(editor.Text == "A😀e\u0301Z\nx", "element deletion retains undo");
            editor.Reset("A😀e\u0301Z\nx", 1);
            editor.Key(UiKey.Delete);
            Check(editor.Text == "Ae\u0301Z\nx", "Delete removes a supplementary element");
            editor.Reset("A😀e\u0301Z\nx", 5);
            editor.Key(UiKey.Backspace);
            Check(editor.Text == "A😀Z\nx", "Backspace removes a combining element");
            editor.Reset("A😀e\u0301Z\nx", 2, 4);
            editor.Key(UiKey.Delete);
            Check(editor.Text == "AZ\nx", "partial selection expands before deletion");
            editor.Reset("A😀e\u0301Z\nx", 4, 2);
            editor.Key(UiKey.C, KeyModifiers.Ctrl);
            Check(editor.Clipboard.Text == "😀e\u0301" && editor.Selection == new UiTextSelection(5, 1),
                "reverse partial selection aligns before copy while preserving direction");
            editor.Reset("A😀Z\nx", 2);
            editor.Frame();
            Check(editor.Caret == 1 && editor.Selection == new UiTextSelection(1, 1), "synchronized caret snaps out of a surrogate pair");
            editor.Reset("😀e\u0301Z", 0);
            editor.Hold(UiKey.RightArrow);
            Check(editor.Caret == 4, "held Right crosses complete source elements");
            editor.Reset("😀e\u0301Z", 0);
            editor.Hold(UiKey.Delete);
            Check(editor.Text == "Z", "held Delete removes complete source elements");
            editor.Reset("e\u0301x", 0);
            editor.Frame(editor.Idle with
            {
                MousePosition = new(26, 30),
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Check(editor.Caret == 2, "pointer near a combining element's trailing edge selects its nearest boundary");
            editor.Reset("\u0301Z", 0);
            editor.Frame(editor.Idle with
            {
                CharEvents = [new('a'), new('b')]
            });
            Check(editor.Text == "a\u0301bZ" && editor.Caret == 3,
                "same-frame characters do not split a new element formed with the existing suffix");
            editor.Reset("ab\n😀x", 1);
            editor.Frame(editor.Idle with
            {
                KeyEvents = [new(UiKey.DownArrow, true, default)]
            });
            editor.State.SetCursor(editor.Id, 1);
            editor.State.SetSelection(editor.Id, new(1, 1));
            editor.State.SetRepeatTime(UiKey.DownArrow, editor.State.TimeSeconds - 1);
            editor.Frame(editor.Idle with
            {
                CharEvents = [new('q')]
            });
            Check(editor.Text is "ab\nq😀x" or "ab\n😀qx", "character input after held vertical movement preserves complete supplementary text");
            editor.Frame(editor.Idle with
            {
                KeyEvents = [new(UiKey.DownArrow, false, default)]
            });
            editor.Reset("\u0301Z", 0);
            editor.Ime.Commit = "a";
            editor.EnableIme();
            editor.Key(UiKey.RightArrow);
            Check(editor.Text == "a\u0301Z" && editor.Caret == 3, "queued IME commit aligns its caret before keyboard navigation");
            editor.Reset("ab\n😀x", 1);
            editor.Key(UiKey.DownArrow);
            Check(editor.Caret is 3 or 5, "vertical target cannot stop inside a supplementary element");
            editor.Reset("ab\n😀x", 0);
            editor.Frame(editor.Idle with
            {
                KeyEvents = [new(UiKey.RightArrow, true, default), new(UiKey.RightArrow, false, default),
                new(UiKey.DownArrow, true, default), new(UiKey.DownArrow, false, default)]
            });
            Check(editor.Caret is 3 or 5, "same-frame navigation leaves a complete source endpoint");
        }
    }
    private sealed class Editor : IDisposable
    {
        private readonly UiContext _context;
        private readonly UiWidget _widget;
        internal readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        internal readonly Clipboard Clipboard = new();
        internal readonly Ime Ime = new();
        internal string Text = "";
        internal string Id
        {
            get;
        }
        internal UiState State => _context.State;
        internal int Caret => Presentation.Last.CaretIndex;
        internal UiTextSelection Selection => Presentation.Last.Selection;
        internal Editor(string mode)
        {
            var glyphs = Enumerable.Range(32, 95).ToDictionary(code => code,
                code => new UiGlyphInfo(7, 0, -10, code == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
            glyphs[0x1f600] = new(14, 0, -10, 13, 12, new(0, 0, 1, 1));
            glyphs[0x301] = new(0, 0, -10, 0, 12, new(0, 0, 1, 1));
            _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255], glyphs,
                new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
            _context.SetClipboard(Clipboard);
            var binding = new UiBinding<string>(() => Text, next => Text = next);
            _widget = mode == "plain" ? new UiMultilineInput<UiStandardMultilineInputBehavior, Presentation>("Elements", binding, 256, 180, 80)
                : new UiMultilineInput<UiFluentMultilineInputBehavior, Presentation>("Elements", binding, 256, 180, 80)
                {
                    Options = new() { Wrap = mode == "wrapped" }
                };
            Frame();
            Id = Presentation.Last.Id;
        }
        internal void EnableIme() => _context.SetImeHandler(Ime);
        internal void Reset(string text, int start, int? end = null)
        {
            State.ActiveId = null;
            Text = text;
            Frame();
            State.ActiveId = State.FocusedId = Id;
            State.SetCursor(Id, end ?? start);
            State.SetSelection(Id, new(start, end ?? start));
            State.RegisterClick("reset-multiline-element-clicks", new(-100, -100));
        }
        internal void Key(UiKey key, KeyModifiers modifiers = default) => Frame(Idle with
        {
            KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)],
            Modifiers = modifiers
        });
        internal void Hold(UiKey key)
        {
            Frame(Idle with
            {
                KeyEvents = [new(key, true, default)]
            });
            State.SetRepeatTime(key, State.TimeSeconds - 1);
            Frame();
            Frame(Idle with
            {
                KeyEvents = [new(key, false, default)]
            });
        }
        internal void Frame(UiInputState? input = null)
        {
            _context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? Idle, new(0, 0, 320, 240), UiTextSettings.Default);
            _context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(20, 20));
                _widget.Render(ui);
            }));
            using var draw = _context.GetDrawData();
        }
        public void Dispose()
        {
            _widget.Dispose();
            _context.Dispose();
        }
    }
    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            new(new(0, 0, width, height), 20)
            {
                Content = new(0, 0, width, height)
            };
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
            new(new(0, 0, size.X, size.Y), 20)
            {
                Content = new(0, 0, size.X, size.Y)
            };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit) => Last = edit;
    }
    internal sealed class Clipboard : IUiClipboard
    {
        internal string Text = "";
        public string GetText() => Text;
        public void SetText(string text) => Text = text;
    }
    internal sealed class Ime : IUiImeHandler
    {
        internal string? Commit;
        public string? GetCompositionText() => null;
        public string? ConsumeCommittedText(string inputId)
        {
            var value = Commit;
            Commit = null;
            return value;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public void SetCompositionOwner(string? inputId)
        {
        }
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float pixelHeight, float pixelWidth)
        {
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
