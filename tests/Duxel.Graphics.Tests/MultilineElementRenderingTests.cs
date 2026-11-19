using System.Numerics;
using Duxel.Core;

internal static class MultilineElementRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiState state, bool vector)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var mode in new[] { "plain-left", "plain-delete", "wrapped-join", "hard-join" })
        {
            var plain = mode.StartsWith("plain", StringComparison.Ordinal);
            var source = mode == "plain-left" ? "e\u0301x" : mode == "plain-delete" ? "😀x" : "\u0301Z";
            var text = source;
            var binding = new UiBinding<string>(() => text, next => text = next);
            using UiWidget editor = plain
                ? new UiMultilineInput<UiStandardMultilineInputBehavior, Presentation>("Element GPU", binding, 256, 180, 80)
                : new UiMultilineInput<UiFluentMultilineInputBehavior, Presentation>("Element GPU", binding, 256, 180, 80)
                    { Options = new() { Wrap = mode == "wrapped-join" } };
            var key = "multi-elements-" + mode + "-" + vector;
            void Scene(UiImmediateContext ui, Action draw)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("multi-elements-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
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
                using var typography = Presentation.BeginText(control);
                using var clip = control.Clip(new(20, 20, 180, 80));
                control.Frame(new(20, 20, 180, 80), new(30, 50, 80), new(100, 160, 210), 0, 1);
                var displayed = plain ? mode == "plain-delete" ? "x" : source : "a\u0301bZ";
                control.TypographyTextBlock(control.MeasureTextBlock(displayed), new(20, 20, 180, 20), new(240, 240, 240));
                var x = plain ? 20 : 20 + control.MeasureText("a\u0301b").X;
                control.Frame(new(MathF.Round(x), 22, 1, 16), new(255, 255, 255), new(0, 0, 0, 0), 0, 0);
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                text = source;
                var keyCode = mode == "plain-left" ? UiKey.LeftArrow : UiKey.Delete;
                setInput(plain ? idle with { KeyEvents = [new(keyCode, true, default), new(keyCode, false, default)] }
                    : idle with { CharEvents = [new('a'), new('b')] });
                var actual = capture(key + "-" + frame, ui => Scene(ui, () =>
                {
                    state.ActiveId = state.FocusedId = id;
                    var cursor = mode == "plain-left" ? 2 : 0;
                    state.SetCursor(id, cursor);
                    state.SetSelection(id, new(cursor, cursor));
                    state.SetScrollX(id, 0);
                    state.SetScrollY(id, 0);
                    new UiControlContext(ui).RestartInputCaretBlink();
                    editor.Render(ui);
                }));
                check(actual.AsSpan().SequenceEqual(expected), "multiline source elements match literal text and caret : " + key + " / " + frame);
            }
        }
        setInput(idle);
    }
    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height) =>
            new(new(0, 0, width, height), 20) { Content = new(0, 0, width, height) };
        public static UiMultilineInputLayout Arrange(UiControlContext context, in UiMultilineInputLayout natural, UiVector2 size) =>
            new(new(0, 0, size.X, size.Y), 20) { Content = new(0, 0, size.X, size.Y) };
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
        {
            Last = edit;
            context.Frame(layout.Field, new(30, 50, 80), new(100, 160, 210), 0, 1);
            context.PaintEditableMultiline(layout, edit, new(new(240, 240, 240), new(60, 100, 160), new(255, 255, 255)) { Typography = true });
        }
    }
}
