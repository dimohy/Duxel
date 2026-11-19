using System.Numerics;
using Duxel.Core;

internal static class MultilineCommandRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiState state, bool vector)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var mode in new[] { "collapse-left", "collapse-right", "word-back", "word-forward" })
        {
            var word = mode is "word-back" or "word-forward";
            var source = word ? "one two\nend" : "ab\ncd";
            var text = source;
            using var editor = new UiMultilineInput<UiFluentMultilineInputBehavior, Presentation>("Command GPU",
                new(() => text, next => text = next), 256, 180, 80) { Options = new() { Wrap = false } };
            var key = "multi-command-" + mode + "-" + vector;
            void Scene(UiImmediateContext ui, Action draw)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("multi-command-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try { ui.SetCursorScreenPos(new(20, 20)); draw(); }
                finally { if (vector) ui.EndLayer(); }
            }
            setInput(idle);
            capture(key + "-prime", ui => Scene(ui, () => { state.ActiveId = null; editor.Render(ui); }));
            var id = Presentation.Last.Id;
            var expected = capture(key + "-reference", ui => Scene(ui, () =>
            {
                state.ActiveId = null;
                var control = new UiControlContext(ui);
                using var typography = control.BeginTypography<UiRegularTypeface, UiFluentType300>();
                using var clip = control.Clip(new(20, 20, 180, 80));
                control.Frame(new(20, 20, 180, 80), new(30, 50, 80), new(100, 160, 210), 0, 1);
                var block = control.MeasureTextBlock(word ? "one \nend" : source);
                control.TypographyTextBlock(block, new(20, 20, 180, 40), new(240, 240, 240));
                var prefix = word ? "one " : mode == "collapse-left" ? "a" : "c";
                var x = MathF.Round(20 + control.MeasureText(prefix).X);
                var y = mode == "collapse-right" ? 42 : 22;
                control.Frame(new(x, y, 1, 16), new(255, 255, 255), new(0, 0, 0, 0), 0, 0);
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                text = source;
                var modifiers = word ? KeyModifiers.Ctrl : default;
                var keyCode = mode == "word-back" ? UiKey.Backspace : mode == "word-forward" ? UiKey.Delete
                    : mode == "collapse-left" ? UiKey.LeftArrow : UiKey.RightArrow;
                setInput(idle with { KeyEvents = [new(keyCode, true, modifiers), new(keyCode, false, modifiers)], Modifiers = modifiers });
                var actual = capture(key + "-" + frame, ui => Scene(ui, () =>
                {
                    state.ActiveId = state.FocusedId = id;
                    var cursor = mode == "word-back" ? 7 : word ? 4 : mode == "collapse-left" ? 4 : 1;
                    state.SetCursor(id, cursor);
                    state.SetSelection(id, word ? new(cursor, cursor) : mode == "collapse-left" ? new(1, 4) : new(4, 1));
                    state.SetScrollX(id, 0);
                    state.SetScrollY(id, 0);
                    new UiControlContext(ui).RestartInputCaretBlink();
                    editor.Render(ui);
                }));
                check(actual.AsSpan().SequenceEqual(expected), "multiline key command matches literal display and caret : " + key + " / " + frame);
            }
        }
        setInput(idle);
    }
    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) =>
            context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            new(new(0, 0, width, height), 20) { Content = new(0, 0, width, height) };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
        {
            Last = edit;
            context.Frame(layout.Field, new(30, 50, 80), new(100, 160, 210), 0, 1);
            context.PaintEditableMultiline(layout, edit, new(new(240, 240, 240), new(60, 100, 160), new(255, 255, 255)) { Typography = true });
        }
    }
}
