using System.Numerics;
using Duxel.Core;

internal static class MultilineWrapRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, UiState state, float scrollbarSize, bool vector)
    {
        foreach (var mode in new[] { "plain", "clip", "scroll", "selection" })
        {
            var text = "aaaa bbbb cccc";
            using var editor = new UiMultilineInput<UiStandardMultilineInputBehavior, Presentation>("Wrapped GPU",
                new(() => text, value => text = value), 1024, height : mode == "scroll" ? 40 : 64);
            var key = "wrapped-" + mode + "-" + vector;
            var id = string.Empty;
            void Scene(UiImmediateContext ui, Action draw)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("wrapped-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try
                {
                    ui.SetCursorScreenPos(new(20, 20));
                    using var clip = new UiControlContext(ui).Clip(mode == "clip" ? new(25, 30, 30, 30) : new(0, 0, 320, 240));
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
                state.ActiveId = !prime && mode == "selection" ? id : null;
                if (!prime)
                {
                    state.SetScrollY(id, mode == "scroll" ? 20 : 0);
                    state.SetScrollX(id, 0);
                    state.SetCursor(id, mode == "selection" ? 12 : text.Length);
                    state.SetSelection(id, mode == "selection" ? new(1, 12) : new(text.Length, text.Length));
                }
                new UiControlContext(ui).RestartInputCaretBlink();
                editor.Render(ui);
            });
            capture(key + "-prime", ui => Actual(ui, true));
            id = Presentation.Last.Id;
            var expected = capture(key + "-reference", ui => Scene(ui, () =>
            {
                var context = new UiControlContext(ui);
                using var typography = Presentation.BeginText(context);
                var width = context.MeasureText("aaaa bbb").X + 1;
                var height = mode == "scroll" ? 40 : 64;
                var root = new UiRect(20, 20, width, height);
                var content = root with { Width = mode == "scroll" ? width - scrollbarSize : width };
                using var rootClip = context.Clip(root);
                context.Frame(root, new(30, 50, 80), new(100, 160, 210), 0, 1);
                using (context.Clip(content))
                {
                    var offset = mode == "scroll" ? 20 : 0;
                    if (mode == "selection")
                    {
                        var left = context.MeasureText("a").X;
                        context.Frame(new(20 + left, 22, context.MeasureText("aaaa ").X - left, 16), new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                        context.Frame(new(20, 42, context.MeasureText("bbbb ").X, 16), new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                        context.Frame(new(20, 62, context.MeasureText("cc").X, 16), new(60, 100, 160), new(0, 0, 0, 0), 0, 0);
                    }
                    foreach (var row in new[] { ("aaaa ", 0), ("bbbb ", 1), ("cccc", 2) })
                    {
                        ui.SetCursorScreenPos(new(20, 20 + row.Item2 * 20 - offset));
                        ui.Text<UiFluentTypographyPresentation<UiRegularTypeface, UiFluentType300>>(row.Item1, new(240, 230, 180));
                    }
                    if (mode == "selection")
                    {
                        var x = MathF.Round(20 + context.MeasureText("cc").X);
                        context.Frame(new(x, 62, 1, 16), new(255, 255, 255), new(0, 0, 0, 0), 0, 0);
                    }
                }
                if (mode == "scroll")
                {
                    var scrollId = key + "-reference-scroll";
                    ui.RenderScrollbarV(scrollId, new(20 + width - scrollbarSize, 20, scrollbarSize, height),
                        20, 20, 60, context.ClipRect);
                }
            }));
            for (var frame = 0; frame < 4; frame++)
            {
                var actual = capture(key + "-" + frame, ui => Actual(ui));
                check(actual.AsSpan().SequenceEqual(expected),
                    "wrapped rows/selection/caret/scroll match independent original-text geometry : " + key + " / " + frame);
            }
            state.ActiveId = null;
        }
    }

    private readonly struct Presentation : IUiMultilineInputPresentation
    {
        internal static UiMultilineEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) =>
            context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiMultilineInputLayout Measure(UiControlContext context, float width, float height)
        {
            var w = context.MeasureText("aaaa bbb").X + 1;
            return new(new(0, 0, w, height), 20) { Content = new(0, 0, w, height), Wrap = true };
        }
        public static void Paint(UiControlContext context, in UiMultilineInputLayout layout, in UiMultilineEditResult edit)
        {
            Last = edit;
            context.Frame(layout.Field, new(30, 50, 80), new(100, 160, 210), 0, 1);
            context.PaintEditableMultiline(layout, edit,
                new(new(240, 230, 180), new(60, 100, 160), new(255, 255, 255)) { Typography = true });
        }
    }
}
