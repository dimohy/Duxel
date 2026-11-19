using Duxel.Core;

internal static class TextCommandTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var mode in new[] { "single", "standard", "wrapped", "hard" })
        {
            using var editor = new Editor(mode);
            void Check(bool condition, string message) => check(condition, mode + " text command : " + message);
            editor.Reset("one e\u0301x tail", 7);
            editor.Key(UiKey.Backspace, KeyModifiers.Ctrl);
            Check(editor.Text == "one  tail" && editor.Caret == 4, "Ctrl Backspace removes the preceding combining word");
            editor.Key(UiKey.Z, KeyModifiers.Ctrl);
            Check(editor.Text == "one e\u0301x tail", "word deletion has an undo entry");
            editor.Key(UiKey.Y, KeyModifiers.Ctrl);
            Check(editor.Text == "one  tail", "word deletion has a redo entry");
            editor.Reset("one e\u0301x tail", 4);
            editor.Key(UiKey.Delete, KeyModifiers.Ctrl);
            Check(editor.Text == "one  tail" && editor.Caret == 4, "Ctrl Delete removes the following combining word");
            editor.Reset("one e\u0301x tail", 3);
            editor.Key(UiKey.Delete, KeyModifiers.Ctrl);
            Check(editor.Text == "one tail", "forward word deletion consumes leading whitespace and the following word");
            editor.Reset("one e\u0301x tail", 8);
            editor.Key(UiKey.Backspace, KeyModifiers.Ctrl);
            Check(editor.Text == "one tail", "backward word deletion consumes trailing whitespace and the preceding word");
            editor.Reset("A😀Z", 1);
            editor.Key(UiKey.Delete, KeyModifiers.Ctrl);
            Check(editor.Text == "AZ", "word deletion preserves complete supplementary elements");
            editor.Reset("one two end", 11);
            editor.Hold(UiKey.Backspace, KeyModifiers.Ctrl);
            Check(editor.Text == "one " && editor.Caret == 4, "held Ctrl Backspace repeats with current modifiers");
            editor.Reset("one two end", 0);
            editor.Hold(UiKey.Delete, KeyModifiers.Ctrl);
            Check(editor.Text == " end" && editor.Caret == 0, "held Ctrl Delete repeats with current modifiers");
            editor.Reset("one two end", 4, 7);
            editor.Key(UiKey.Delete, KeyModifiers.Ctrl);
            Check(editor.Text == "one  end", "Ctrl Delete deletes only an existing selection");
            editor.Reset("one two end", 7, 4);
            editor.Key(UiKey.Backspace, KeyModifiers.Ctrl);
            Check(editor.Text == "one  end", "Ctrl Backspace deletes only a reverse selection");
            editor.Reset("one", 0);
            var writes = editor.Writes;
            editor.Key(UiKey.Backspace, KeyModifiers.Ctrl);
            editor.Select(3, 3);
            editor.Key(UiKey.Delete, KeyModifiers.Ctrl);
            Check(editor.Text == "one" && editor.Writes == writes, "word deletion at source endpoints does not write the binding");
            editor.ReadOnly = true;
            editor.Reset("one two", 4);
            writes = editor.Writes;
            editor.Key(UiKey.Delete, KeyModifiers.Ctrl);
            editor.Hold(UiKey.Backspace, KeyModifiers.Ctrl);
            Check(editor.Text == "one two" && editor.Writes == writes, "readonly guards apply to word deletion and repetition");
            editor.ReadOnly = false;
            if (mode == "single")
                continue;
            editor.Reset("ab\ncd", 1, 4);
            editor.Key(UiKey.LeftArrow);
            Check(editor.Caret == 1 && !editor.Selection.HasSelection, "Left collapses a cross-line selection to its leading endpoint");
            editor.Reset("ab\ncd", 4, 1);
            editor.Key(UiKey.RightArrow);
            Check(editor.Caret == 4 && !editor.Selection.HasSelection, "Right collapses a reverse cross-line selection to its trailing endpoint");
            editor.Reset("ab\ncd", 1, 4);
            editor.Key(UiKey.LeftArrow, KeyModifiers.Shift);
            Check(editor.Selection == new UiTextSelection(1, 3), "Shift Left extends from the active endpoint");
            editor.Reset("ab\ncd", 0);
            editor.Frame(editor.Idle with { KeyEvents = [new(UiKey.RightArrow, true, default)] });
            editor.Select(4, 1);
            editor.State.SetRepeatTime(UiKey.RightArrow, editor.State.TimeSeconds - 1);
            editor.Frame();
            Check(editor.Caret == 4 && !editor.Selection.HasSelection, "held Right collapses the current selection");
            editor.Frame(editor.Idle with { KeyEvents = [new(UiKey.RightArrow, false, default)] });
            editor.Reset("one\ntwo", 3);
            editor.Key(UiKey.Delete, KeyModifiers.Ctrl);
            Check(editor.Text == "one", "forward word deletion crosses a hard line break");
            editor.Reset("one\ntwo", 4);
            editor.Key(UiKey.Backspace, KeyModifiers.Ctrl);
            Check(editor.Text == "two", "backward word deletion crosses a hard line break");
        }
    }

    private sealed class Editor : IDisposable
    {
        private readonly UiContext _context;
        private readonly UiInput<UiStandardTextInputBehavior, SinglePresentation>? _single;
        private readonly UiMultilineInput<UiStandardMultilineInputBehavior, MultiPresentation>? _standard;
        private readonly UiMultilineInput<UiFluentMultilineInputBehavior, MultiPresentation>? _fluent;
        internal readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        internal string Text = "";
        internal int Writes;
        internal string Id { get; }
        internal UiState State => _context.State;
        internal int Caret => _single is not null ? SinglePresentation.Last.CaretIndex : MultiPresentation.Last.CaretIndex;
        internal UiTextSelection Selection => _single is not null ? SinglePresentation.Last.Selection : MultiPresentation.Last.Selection;
        internal bool ReadOnly
        {
            set
            {
                var flags = value ? UiInputTextFlags.ReadOnly : UiInputTextFlags.None;
                if (_single is not null) _single.Options = new(flags);
                if (_standard is not null) _standard.Options = _standard.Options with { Flags = flags };
                if (_fluent is not null) _fluent.Options = _fluent.Options with { Flags = flags };
            }
        }
        internal Editor(string mode)
        {
            var glyphs = Enumerable.Range(32, 95).ToDictionary(code => code,
                code => new UiGlyphInfo(7, 0, -10, code == 32 ? 0 : 6, 12, new(0, 0, 1, 1)));
            glyphs[0x1f600] = new(14, 0, -10, 13, 12, new(0, 0, 1, 1));
            glyphs[0x301] = new(0, 0, -10, 0, 12, new(0, 0, 1, 1));
            _context = new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255], glyphs,
                new Dictionary<uint, float>(), 12, -3, 1, -1), new(1), new(2));
            var binding = new UiBinding<string>(() => Text, next => { Text = next; Writes++; });
            if (mode == "single") _single = new("Commands", binding, 256, 180);
            else if (mode == "standard") _standard = new("Commands", binding, 256, 180, 80);
            else _fluent = new("Commands", binding, 256, 42, 80) { Options = new() { Wrap = mode == "wrapped" } };
            Frame();
            Id = _single is not null ? SinglePresentation.Last.Id : MultiPresentation.Last.Id;
        }
        internal void Reset(string text, int start, int? end = null)
        {
            State.ActiveId = null;
            Text = text;
            Frame();
            State.ActiveId = State.FocusedId = Id;
            Select(start, end ?? start);
        }
        internal void Select(int start, int end) { State.SetSelection(Id, new(start, end)); State.SetCursor(Id, end); }
        internal void Key(UiKey key, KeyModifiers modifiers = default) => Frame(Idle with
        { KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)], Modifiers = modifiers });
        internal void Hold(UiKey key, KeyModifiers modifiers)
        {
            Frame(Idle with { KeyEvents = [new(key, true, modifiers)], Modifiers = modifiers });
            State.SetRepeatTime(key, State.TimeSeconds - 1);
            Frame(Idle with { Modifiers = modifiers });
            Frame(Idle with { KeyEvents = [new(key, false, modifiers)], Modifiers = modifiers });
        }
        internal void Frame(UiInputState? input = null)
        {
            _context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? Idle, new(0, 0, 320, 240), UiTextSettings.Default);
            _context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(20, 20));
                if (_single is not null) _single.Render(ui);
                else if (_standard is not null) _standard.Render(ui);
                else _fluent!.Render(ui);
            }));
            using var draw = _context.GetDrawData();
        }
        public void Dispose() { _single?.Dispose(); _standard?.Dispose(); _fluent?.Dispose(); _context.Dispose(); }
    }
    private readonly struct SinglePresentation : IUiInputPresentation
    {
        internal static UiInputEditResult Last;
        public static UiInputLayout Measure(UiControlContext context, string label, float width) =>
            new(new(width, 32), new(0, 0, width, 32), new(0, 6, width, 20), default, 20);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit) => Last = edit;
    }
    private readonly struct MultiPresentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            new(new(0, 0, width, height), 20) { Content = new(0, 0, width, height) };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit) => Last = edit;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
