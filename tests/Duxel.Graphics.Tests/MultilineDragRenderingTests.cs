using System.Numerics;
using Duxel.Core;

internal static class MultilineDragRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiState state, bool vector)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var mode in new[] { "keyboard", "right", "left" })
        {
            var value = "ab\ncd";
            using var editor = new UiMultilineInput<UiFluentMultilineInputBehavior, Presentation>("Drag GPU",
                new(() => value, next => value = next), 128, 180, 80) { Options = new() { Wrap = false } };
            var key = "multi-drag-" + mode + "-" + vector;
            void Scene(UiImmediateContext ui, Action draw)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("multi-drag-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try { ui.SetCursorScreenPos(new(20, 20)); draw(); }
                finally { if (vector) ui.EndLayer(); }
            }
            setInput(idle);
            capture(key + "-prime", ui => Scene(ui, () => { state.ActiveId = null; editor.Render(ui); }));
            var id = Presentation.Last.Id;
            var pressX = 0f;
            var expected = capture(key + "-reference", ui => Scene(ui, () =>
            {
                state.ActiveId = null;
                var control = new UiControlContext(ui);
                using var typography = Presentation.BeginText(control);
                using var clip = control.Clip(new(20, 20, 180, 80));
                control.Frame(new(20, 20, 180, 80), new(30, 50, 80), new(100, 160, 210), 0, 1);
                var a = control.MeasureText("a").X;
                var cd = control.MeasureText("cd").X;
                pressX = 20 + a;
                if (mode == "left")
                    control.Frame(new(20, 22, a, 16), new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                if (mode == "right")
                {
                    control.Frame(new(20 + a, 22, control.MeasureText("ab ").X - a, 16), new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                    control.Frame(new(20, 42, cd, 16), new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                }
                control.TypographyTextBlock(control.MeasureTextBlock("ab\ncd"), new(20, 20, 180, 40), new(240, 240, 240));
                var x = mode == "right" ? 20 + cd : mode == "left" ? 20 : pressX;
                control.Frame(new(MathF.Round(x), mode == "right" ? 42 : 22, 1, 16), new(255, 255, 255), new(0, 0, 0, 0), 0, 0);
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                if (mode != "keyboard")
                {
                    state.RegisterClick("reset-multi-drag-gpu-clicks", new(-100, -100));
                    setInput(idle with { MousePosition = new(pressX, 25), LeftMouseDown = true, LeftMousePressed = true });
                    capture(key + "-press-" + frame, ui => Scene(ui, () =>
                    {
                        state.ActiveId = null;
                        state.SetCursor(id, 1);
                        state.SetSelection(id, new(1, 1));
                        editor.Render(ui);
                    }));
                }
                setInput(idle with { MousePosition = mode == "left" ? new(-100, -100) : new(250, 250), LeftMouseDown = true });
                var actual = capture(key + "-" + frame, ui => Scene(ui, () =>
                {
                    if (mode == "keyboard")
                    {
                        state.ActiveId = state.FocusedId = id;
                        state.SetCursor(id, 1);
                        state.SetSelection(id, new(1, 1));
                    }
                    new UiControlContext(ui).RestartInputCaretBlink();
                    editor.Render(ui);
                }));
                check(actual.AsSpan().SequenceEqual(expected), "multiline pointer ownership matches literal selection and caret : " + key + " / " + frame);
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
