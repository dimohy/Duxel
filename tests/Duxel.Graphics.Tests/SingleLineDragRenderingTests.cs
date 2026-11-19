using System.Numerics;
using Duxel.Core;

internal static class SingleLineDragRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiState state, bool vector)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var mode in new[] { "right", "left", "scroll" })
        {
            var source = mode == "scroll" ? "WWWWWW" : "abcdef";
            var value = source;
            using var editor = new UiInput<UiFluentTextInputBehavior, Presentation>("Drag GPU", new(() => value, next => value = next), 128, 80);
            var key = "single-drag-" + mode + "-" + vector;
            void Scene(UiImmediateContext ui, Action draw)
            {
                if (vector)
                    ui.BeginLayer("single-drag-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try { ui.SetCursorScreenPos(new(20, 20)); draw(); }
                finally { if (vector) ui.EndLayer(); }
            }
            setInput(idle);
            capture(key + "-prime", ui => Scene(ui, () => { state.ActiveId = null; editor.Render(ui); }));
            var id = Presentation.Last.Id;
            var pressX = 0f;
            var expected = capture(key + "-reference", ui => Scene(ui, () =>
            {
                var control = new UiControlContext(ui);
                using var typography = Presentation.BeginText(control);
                using var clip = control.Clip(new(20, 20, 80, 32));
                control.Frame(new(20, 20, 80, 32), new(30, 50, 80), new(100, 160, 210), 0, 1);
                var prefix = control.MeasureText(source[..1]).X;
                var full = control.MeasureText(source).X;
                var scroll = mode == "scroll" ? MathF.Max(0, full - 68) : 0;
                if (mode == "scroll" && (scroll <= 0 || scroll > 40))
                    throw new InvalidOperationException("The selected font must overflow the drag fixture by at most 40 pixels.");
                pressX = 26 + prefix;
                var start = mode == "left" ? 0 : prefix;
                var end = mode == "left" ? prefix : full;
                control.Frame(new(26 + start - scroll, 26, end - start, 20), new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                control.TypographyTextBlock(control.MeasureTextBlock(source), new(26 - scroll, 26, 68, 20), new(240, 240, 240));
                var caret = mode == "left" ? 0 : full;
                control.Frame(new(MathF.Round(26 + caret - scroll), 27, 1, 18), new(255, 255, 255), new(0, 0, 0, 0), 0, 0);
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                state.RegisterClick("reset-drag-gpu-clicks", new(-100, -100));
                setInput(idle with { MousePosition = new(pressX, 30), LeftMouseDown = true, LeftMousePressed = true });
                capture(key + "-press-" + frame, ui => Scene(ui, () =>
                {
                    state.ActiveId = null;
                    state.SetCursor(id, 0);
                    state.SetSelection(id, default);
                    state.SetScrollX(id, 0);
                    editor.Render(ui);
                }));
                setInput(idle with { MousePosition = new(mode == "left" ? -100 : 250, -50), LeftMouseDown = true });
                byte[] actual = [];
                for (var step = 0; step < (mode == "scroll" ? 4 : 1); step++)
                    actual = capture(key + "-" + frame + "-step-" + step, ui => Scene(ui, () =>
                    {
                        new UiControlContext(ui).RestartInputCaretBlink();
                        editor.Render(ui);
                    }));
                check(actual.AsSpan().SequenceEqual(expected), "actual outside drag matches literal selection,scroll and caret : " + key + " / " + frame);
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
