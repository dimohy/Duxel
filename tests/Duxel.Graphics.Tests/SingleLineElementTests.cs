using Duxel.Core;

internal static class SingleLineElementTests
{
    internal static void Run(Action<bool, string> check)
    {
        using (var editor = new Editor("A😀e\u0301Z"))
        {
            editor.Select(5, 5);
            editor.Key(UiKey.LeftArrow);
            check(editor.Caret == 3, "single-line Left crosses a complete combining element");
            editor.Key(UiKey.LeftArrow);
            check(editor.Caret == 1, "single-line Left crosses a complete supplementary element");
            editor.Key(UiKey.RightArrow);
            check(editor.Caret == 3, "single-line Right crosses a complete supplementary element");
            editor.Key(UiKey.RightArrow, KeyModifiers.Shift);
            check(editor.Selection == new UiTextSelection(3, 5), "single-line Shift Right selects a complete combining element");
            editor.Key(UiKey.C, KeyModifiers.Ctrl);
            check(editor.Clipboard.Text == "e\u0301", "single-line copy uses complete selected text elements");
            editor.Key(UiKey.Delete);
            check(editor.Text == "A😀Z", "single-line selected deletion preserves surrounding supplementary text");
            editor.Key(UiKey.Z, KeyModifiers.Ctrl);
            check(editor.Text == "A😀e\u0301Z", "single-line text-element selected deletion retains undo");
            editor.Select(1, 1);
            editor.Key(UiKey.Delete);
            check(editor.Text == "Ae\u0301Z", "single-line Delete removes an entire supplementary element");
            editor.Key(UiKey.Z, KeyModifiers.Ctrl);
            editor.Select(5, 5);
            editor.Key(UiKey.Backspace);
            check(editor.Text == "A😀Z", "single-line Backspace removes an entire combining element");
        }
        using (var editor = new Editor("A😀e\u0301Z"))
        {
            editor.Select(2, 4);
            editor.Key(UiKey.Delete);
            check(editor.Text == "AZ", "single-line partial source selection expands to whole elements before deletion");
        }
        using (var editor = new Editor("A😀e\u0301Z"))
        {
            editor.Select(4, 2);
            editor.Key(UiKey.C, KeyModifiers.Ctrl);
            check(editor.Clipboard.Text == "😀e\u0301" && editor.Selection == new UiTextSelection(5, 1),
                "single-line reverse selection preserves direction while aligning both element boundaries");
        }
        using (var editor = new Editor("A😀Z"))
        {
            editor.Select(2, 2);
            editor.Frame();
            check(editor.Caret == 1 && editor.Selection == new UiTextSelection(1, 1),
                "single-line synchronized caret inside a surrogate pair snaps to its leading boundary");
        }
        using (var editor = new Editor("A😀e\u0301Z"))
        {
            editor.Select(1, 5);
            editor.Key(UiKey.LeftArrow);
            check(editor.Caret == 1 && !editor.Selection.HasSelection,
                "single-line Left collapses an existing selection to its leading boundary");
        }
        using (var editor = new Editor("A😀e\u0301Z"))
        {
            editor.Select(5, 1);
            editor.Key(UiKey.RightArrow);
            check(editor.Caret == 5 && !editor.Selection.HasSelection,
                "single-line Right collapses a reverse selection to its trailing boundary");
        }
        using (var editor = new Editor("A😀e\u0301Z"))
        {
            editor.Widget.Options = new(UiInputTextFlags.ReadOnly);
            editor.Select(2, 4);
            editor.Key(UiKey.C, KeyModifiers.Ctrl);
            editor.Key(UiKey.Delete);
            check(editor.Text == "A😀e\u0301Z" && editor.Clipboard.Text == "😀e\u0301",
                "readonly single-line copy aligns elements while deletion remains disabled");
        }
        using (var editor = new Editor("e\u0301x"))
        {
            editor.Click(26);
            check(editor.Caret == 2, "single-line pointer near a combining element's trailing edge returns its trailing boundary");
        }
        using (var editor = new Editor("👩\u200d💻x"))
        {
            editor.Click(30);
            check(editor.Caret == 0, "single-line pointer uses the leading half of an entire joined element");
        }
        using (var editor = new Editor("👩\u200d💻x"))
        {
            editor.Click(40);
            check(editor.Caret == 5, "single-line pointer uses the trailing half of an entire joined element");
        }
        using (var editor = new Editor("e\u0301x y"))
        {
            editor.Select(0, 0);
            editor.Key(UiKey.RightArrow, KeyModifiers.Ctrl);
            check(editor.Caret == 3, "single-line word movement includes combining marks in a letter run");
            editor.Key(UiKey.LeftArrow, KeyModifiers.Ctrl);
            check(editor.Caret == 0, "single-line reverse word movement returns to the complete letter run");
        }
        using (var editor = new Editor("α+β"))
        {
            editor.Select(0, 0);
            editor.Key(UiKey.RightArrow, KeyModifiers.Ctrl);
            check(editor.Caret == 1, "single-line word classification distinguishes Unicode letters from adjacent punctuation");
        }
        using (var editor = new Editor("😀e\u0301Z"))
        {
            editor.Select(0, 0);
            editor.Frame(editor.Idle with { KeyEvents = [new(UiKey.RightArrow, true, default)] });
            editor.State.SetRepeatTime(UiKey.RightArrow, editor.State.TimeSeconds - 1);
            editor.Frame();
            check(editor.Caret == 4, "held single-line Right repeats over complete text elements");
        }
        using (var editor = new Editor("😀e\u0301Z"))
        {
            editor.Select(0, 0);
            editor.Frame(editor.Idle with { KeyEvents = [new(UiKey.Delete, true, default)] });
            editor.State.SetRepeatTime(UiKey.Delete, editor.State.TimeSeconds - 1);
            editor.Frame();
            check(editor.Text == "Z", "held single-line Delete repeats over complete text elements");
        }
        using (var editor = new Editor("\u0301Z"))
        {
            editor.Select(0, 0);
            editor.Frame(editor.Idle with { CharEvents = [new('a'), new('b')] });
            check(editor.Text == "a\u0301bZ", "single-frame character input preserves a cluster formed with the existing suffix");
        }
        using (var editor = new Editor("a"))
        {
            editor.Text = "😀e\u0301";
            editor.Frame();
            editor.Select(4, 4);
            editor.Key(UiKey.LeftArrow);
            check(editor.Caret == 2, "external model replacement invalidates single-line element boundaries");
            editor.Text = "ascii";
            editor.Frame();
            editor.Select(5, 5);
            editor.Key(UiKey.LeftArrow);
            check(editor.Caret == 4, "single-line element navigation remains correct after an ASCII replacement");
            editor.Widget.Dispose();
            check(editor.State.TextInputStorage == (0, 0), "single-line disposal releases the buffer that owns optional text-element boundaries");
        }
    }
    private sealed class Editor : IDisposable
    {
        private readonly UiContext _context;
        internal readonly Clipboard Clipboard = new();
        internal readonly UiInput<UiStandardTextInputBehavior, Presentation> Widget;
        internal readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        internal string Text;
        internal string Id { get; }
        internal UiState State => _context.State;
        internal int Caret => Presentation.Last.CaretIndex;
        internal UiTextSelection Selection => Presentation.Last.Selection;
        internal Editor(string text)
        {
            Text = text;
            var glyphs = Enumerable.Range(32, 95).ToDictionary(code => code,
                code => new UiGlyphInfo(7, 0, -10, code == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
            foreach (var rune in new[] { 0x1f600, 0x1f469, 0x1f4bb }) glyphs[rune] = new(14, 0, -10, 13, 12, new(0, 0, 1, 1));
            foreach (var rune in new[] { 0x301, 0x200d }) glyphs[rune] = new(0, 0, -10, 0, 12, new(0, 0, 1, 1));
            _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255], glyphs,
                new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
            _context.SetClipboard(Clipboard);
            Widget = new("Elements", new(() => Text, next => Text = next), 256, 180);
            Frame();
            Id = Presentation.Last.Id;
            State.ActiveId = Id;
        }
        internal void Frame(UiInputState? input = null)
        {
            _context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? Idle, new(0, 0, 320, 240), UiTextSettings.Default);
            _context.Render(new Screen(ui => { ui.SetCursorScreenPos(new(20, 20)); Widget.Render(ui); }));
            using var draw = _context.GetDrawData();
        }
        internal void Select(int start, int end) { State.SetSelection(Id, new(start, end)); State.SetCursor(Id, end); }
        internal void Key(UiKey key, KeyModifiers modifiers = default) => Frame(Idle with
        { KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)], Modifiers = modifiers });
        internal void Click(float x) => Frame(Idle with { MousePosition = new(x, 30), LeftMouseDown = true, LeftMousePressed = true });
        public void Dispose() { Widget.Dispose(); _context.Dispose(); }
    }
    private readonly struct Presentation : IUiInputPresentation
    {
        internal static UiInputEditResult Last;
        public static UiInputLayout Measure(UiControlContext context, string label, float width) =>
            new(new(width, 32), new(0, 0, width, 32), new(0, 6, width, 20), default, 20);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit)
        {
            Last = edit;
            context.PaintEditableText(layout, edit, new(new(240, 240, 240), new(60, 100, 160), new(255, 255, 255)));
        }
    }
    internal sealed class Clipboard : IUiClipboard
    {
        internal string Text = "";
        public string GetText() => Text;
        public void SetText(string text) => Text = text;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
