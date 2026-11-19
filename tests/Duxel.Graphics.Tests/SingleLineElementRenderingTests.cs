using System.Numerics;
using Duxel.Core;

internal static class SingleLineElementRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiState state, bool vector)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var mode in new[] { "left", "selection", "delete", "word-back", "word-forward" })
        {
            var word = mode is "word-back" or "word-forward";
            var source = word ? "one two" : mode == "left" ? "e\u0301x" : mode == "selection" ? "Ae\u0301Z" : "😀x";
            var text = source;
            using var editor = new UiInput<UiStandardTextInputBehavior, Presentation>("Element GPU", new(() => text, next => text = next), 256, 180);
            var id = "";
            var key = "single-elements-" + mode + "-" + vector;
            void Scene(UiImmediateContext ui, Action draw)
            {
                if (vector)
                    ui.BeginLayer("single-elements-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try { ui.SetCursorScreenPos(new(20, 20)); draw(); }
                finally { if (vector) ui.EndLayer(); }
            }
            setInput(idle);
            capture(key + "-prime", ui => Scene(ui, () => { state.ActiveId = null; editor.Render(ui); }));
            id = Presentation.Last.Id;
            var expected = capture(key + "-reference", ui => Scene(ui, () =>
            {
                state.ActiveId = null;
                var control = new UiControlContext(ui);
                using var typography = Presentation.BeginText(control);
                using var clip = control.Clip(new(20, 20, 180, 32));
                control.Frame(new(20, 20, 180, 32), new(30, 50, 80), new(100, 160, 210), 0, 1);
                var displayed = word ? "one " : mode == "delete" ? "x" : source;
                var caret = 26f;
                if (word)
                    caret += control.MeasureText("one ").X;
                if (mode == "selection")
                {
                    var start = control.MeasureText("A").X;
                    var end = control.MeasureText("Ae\u0301").X;
                    control.Frame(new(26 + start, 26, end - start, 20), new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                    caret += end;
                }
                var block = control.MeasureTextBlock(displayed);
                control.TypographyTextBlock(block, new(26, 26, 168, 20), new(240, 240, 240));
                control.Frame(new(MathF.Round(caret), 27, 1, 18), new(255, 255, 255), new(0, 0, 0, 0), 0, 0);
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                text = source;
                var modifiers = word ? KeyModifiers.Ctrl : mode == "selection" ? KeyModifiers.Shift : default;
                var keyCode = mode == "word-back" ? UiKey.Backspace
                    : mode == "left" ? UiKey.LeftArrow : mode == "selection" ? UiKey.RightArrow : UiKey.Delete;
                setInput(idle with { KeyEvents = [new(keyCode, true, modifiers), new(keyCode, false, modifiers)], Modifiers = modifiers });
                var actual = capture(key + "-" + frame, ui => Scene(ui, () =>
                {
                    state.ActiveId = id;
                    var cursor = mode == "word-back" ? 7 : mode == "word-forward" ? 4
                        : mode == "left" ? 2 : mode == "selection" ? 1 : 0;
                    state.SetCursor(id, cursor);
                    state.SetSelection(id, new(cursor, cursor));
                    state.SetScrollX(id, 0);
                    new UiControlContext(ui).RestartInputCaretBlink();
                    editor.Render(ui);
                }));
                check(actual.AsSpan().SequenceEqual(expected), "single-line element editing matches literal text,selection and caret : " + key + " / " + frame);
            }
        }
        setInput(idle);
    }
    private readonly struct Presentation : IUiInputPresentation
    {
        internal static UiInputEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiInputLayout Measure(UiControlContext context, string label, float width) =>
            new(new(width, 32), new(0, 0, width, 32), new(6, 6, width - 12, 20), default, 20);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit)
        {
            Last = edit;
            context.Frame(layout.Field, new(30, 50, 80), new(100, 160, 210), 0, 1);
            context.PaintEditableText(layout, edit, new(new(240, 240, 240), new(60, 100, 160), new(255, 255, 255)) { Typography = true });
        }
    }
}
