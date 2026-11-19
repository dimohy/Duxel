using System.Numerics;
using Duxel.Core;

internal static class MultilineHardLineRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, UiState state, float scrollbarSize, bool vector)
    {
        foreach (var mode in new[] { "plain", "clip", "selection", "scroll", "edge-caret" })
        {
            var scrollable = mode is "scroll" or "edge-caret";
            var text = scrollable ? "wwwwwwwwwwww\r\nc\rd\nxy\n" : "ab\r\nc\rd\nxy\n";
            var width = scrollable ? 70 : 120;
            var height = scrollable ? 48 : 108;
            using var editor = new UiMultilineInput<UiStandardMultilineInputBehavior, Presentation>("Hard GPU",
                new(() => text, next => text = next), 4096, width, height)
            {
                Options = new() { Flags = UiInputTextFlags.ReadOnly, Wrap = false }
            };
            var key = "hard-lines-" + mode + "-" + vector;
            var id = "";
            void Scene(UiImmediateContext ui, Action draw)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("hard-lines-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try
                {
                    ui.SetCursorScreenPos(new(20, 20));
                    using var clip = new UiControlContext(ui).Clip(mode == "clip" ? new(25, 30, 50, 45) : new(0, 0, 320, 240));
                    draw();
                }
                finally
                {
                    if (vector)
                        ui.EndLayer();
                }
            }
            void Actual(UiImmediateContext ui, bool prime = false) => Scene(ui, () =>
            {
                state.ActiveId = !prime && mode is "selection" or "edge-caret" ? id : null;
                if (!prime)
                {
                    state.SetCursor(id, mode == "selection" ? 9 : mode == "edge-caret" ? 12 : 0);
                    state.SetSelection(id, mode == "selection" ? new(1, 9) : default);
                    if (mode != "edge-caret")
                    {
                        state.SetScrollX(id, mode == "scroll" ? 10 : 0);
                        state.SetScrollY(id, mode == "scroll" ? 20 : 0);
                    }
                }
                new UiControlContext(ui).RestartInputCaretBlink();
                editor.Render(ui);
            });
            capture(key + "-prime", ui => Actual(ui, true));
            id = Presentation.Last.Id;
            var expected = capture(key + "-reference", ui => Scene(ui, () =>
            {
                var control = new UiControlContext(ui);
                using var typography = Presentation.BeginText(control);
                var root = new UiRect(20, 20, width, height);
                var viewport = root with
                {
                    Width = width - (scrollable ? scrollbarSize : 0),
                    Height = height - (scrollable ? scrollbarSize : 0)
                };
                var extentWidth = control.MeasureText("wwwwwwwwwwww").X;
                var offset = mode == "scroll" ? new UiVector2(10, 20)
                    : mode == "edge-caret" ? new(extentWidth - viewport.Width, 0) : default;
                using var rootClip = control.Clip(root);
                control.Frame(root, new(30, 50, 80), new(100, 160, 210), 0, 1);
                using (control.Clip(viewport))
                {
                    if (mode == "selection")
                    {
                        var a = control.MeasureText("a").X;
                        foreach (var row in new[] { (0, a, control.MeasureText("ab ").X - a), (1, 0f, control.MeasureText("c ").X),
                            (2, 0f, control.MeasureText("d ").X), (3, 0f, control.MeasureText("x").X) })
                            control.Frame(new(20 + row.Item2, 22 + row.Item1 * 20, row.Item3, 16),
                                new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                    }
                    var display = scrollable ? "wwwwwwwwwwww\nc\nd\nxy\n" : "ab\nc\nd\nxy\n";
                    var block = control.MeasureTextBlock(display);
                    control.TypographyTextBlock(block, new(20 - offset.X, 20 - offset.Y, width, 100), new(240, 240, 240));
                    if (mode == "selection")
                        control.Frame(new(MathF.Round(20 + control.MeasureText("x").X), 82, 1, 16),
                            new(255, 255, 255), new(0, 0, 0, 0), 0, 0);
                    if (mode == "edge-caret")
                        control.Frame(new(viewport.X + viewport.Width - 1, 22, 1, 16),
                            new(255, 255, 255), new(0, 0, 0, 0), 0, 0);
                }
                if (scrollable)
                {
                    var vertical = key + "-reference-v";
                    var horizontal = key + "-reference-h";
                    ui.RenderScrollbarV(vertical, new(20 + width - scrollbarSize, 20, scrollbarSize, height - scrollbarSize),
                        offset.Y, 100 - viewport.Height, 100, control.ClipRect);
                    ui.RenderScrollbarH(horizontal, new(20, 20 + height - scrollbarSize, width - scrollbarSize, scrollbarSize),
                        offset.X, extentWidth - viewport.Width, extentWidth, control.ClipRect);
                    control.Frame(new(20 + width - scrollbarSize, 20 + height - scrollbarSize, scrollbarSize, scrollbarSize),
                        control.Theme.ScrollbarBg, new(0, 0, 0, 0), 0, 0);
                }
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                var actual = capture(key + "-" + frame, ui => Actual(ui));
                check(actual.AsSpan().SequenceEqual(expected), "hard lines match independent CR/LF rows,selection,caret and both scrollbars : " + key + " / " + frame);
            }
        }
    }
    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) =>
            context.BeginTypography<UiRegularTypeface, UiFluentType300>();
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
